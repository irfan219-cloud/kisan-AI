using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using KisanMitraAI.Core.Advisory;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace KisanMitraAI.Infrastructure.Advisory;

/// <summary>
/// Cost-optimized implementation of IKnowledgeBaseService that uses direct Bedrock InvokeModel API
/// instead of OpenSearch-based Knowledge Base RAG. Reduces costs by $161/week by eliminating
/// OpenSearch Serverless while maintaining advisory functionality through prompt engineering.
/// </summary>
public class DirectBedrockKnowledgeBaseService : IKnowledgeBaseService
{
    private readonly IAmazonBedrockRuntime _bedrockRuntime;
    private readonly ILogger<DirectBedrockKnowledgeBaseService> _logger;
    
    // Amazon Nova Pro model (no payment instrument issues, latest generation)
    private const string ModelId = "us.amazon.nova-pro-v1:0";
    
    // Agricultural domain context embedded in prompts to compensate for lack of vector search
    private const string AgriculturalContext = @"
You are an expert agricultural advisor with deep knowledge of:
- Indian farming practices and regional variations
- Crop types: wheat, rice, cotton, sugarcane, pulses, oilseeds, vegetables, fruits
- Soil health: pH levels, NPK nutrients, organic carbon, micronutrients
- Regenerative farming: cover cropping, crop rotation, composting, reduced tillage
- Irrigation methods: drip, sprinkler, flood, rainfed
- Pest and disease management using integrated approaches
- Weather patterns and climate considerations for Indian agriculture
- Government schemes: PM-KISAN, Soil Health Card, crop insurance
- Market prices and value chain optimization

Provide practical, actionable advice tailored to small and marginal farmers in India.
Use simple language and focus on cost-effective, sustainable solutions.";

    public DirectBedrockKnowledgeBaseService(
        IAmazonBedrockRuntime bedrockRuntime,
        ILogger<DirectBedrockKnowledgeBaseService> logger)
    {
        _bedrockRuntime = bedrockRuntime ?? throw new ArgumentNullException(nameof(bedrockRuntime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<KnowledgeBaseResponse> QueryKnowledgeBaseAsync(
        string query,
        string context,
        int maxResults,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Querying Bedrock directly with query: {Query}", query);

            // Build prompt with agricultural domain context
            var prompt = BuildPromptWithContext(query, context);

            // Invoke Bedrock InvokeModel API with exponential backoff retry
            var response = await InvokeBedrockWithRetryAsync(prompt, cancellationToken);

            // Parse response and format as KnowledgeBaseResponse
            var answer = ExtractAnswerFromResponse(response);
            var citations = GenerateSyntheticCitations(answer);
            var confidenceScore = CalculateConfidenceScore(answer, response);

            _logger.LogInformation(
                "Bedrock query completed. Answer length: {Length}, Confidence: {Confidence}",
                answer.Length,
                confidenceScore);

            return new KnowledgeBaseResponse(
                Answer: answer,
                Citations: citations,
                ConfidenceScore: confidenceScore
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying Bedrock directly");
            throw;
        }
    }

    private string BuildPromptWithContext(string query, string additionalContext)
    {
        var promptBuilder = new StringBuilder();
        
        // Add agricultural domain context
        promptBuilder.AppendLine(AgriculturalContext);
        promptBuilder.AppendLine();
        
        // Add any additional context provided by the caller
        if (!string.IsNullOrWhiteSpace(additionalContext))
        {
            promptBuilder.AppendLine("Additional Context:");
            promptBuilder.AppendLine(additionalContext);
            promptBuilder.AppendLine();
        }
        
        // Add the user's query
        promptBuilder.AppendLine("Farmer's Question:");
        promptBuilder.AppendLine(query);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Please provide a detailed, practical answer:");

        return promptBuilder.ToString();
    }

    private async Task<string> InvokeBedrockWithRetryAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        const int baseDelayMs = 1000;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // Build Amazon Nova request body
                var requestBody = new
                {
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    inferenceConfig = new
                    {
                        maxTokens = 2048,
                        temperature = 0.7
                    }
                };

                var requestBodyJson = JsonSerializer.Serialize(requestBody);
                var requestBodyBytes = Encoding.UTF8.GetBytes(requestBodyJson);

                var request = new InvokeModelRequest
                {
                    ModelId = ModelId,
                    Body = new MemoryStream(requestBodyBytes),
                    ContentType = "application/json",
                    Accept = "application/json"
                };

                _logger.LogDebug("Invoking Bedrock model {ModelId}, attempt {Attempt}", ModelId, attempt + 1);

                var response = await _bedrockRuntime.InvokeModelAsync(request, cancellationToken);

                // Parse response
                using var reader = new StreamReader(response.Body);
                var responseJson = await reader.ReadToEndAsync(cancellationToken);
                
                return responseJson;
            }
            catch (Amazon.BedrockRuntime.Model.ThrottlingException ex)
            {
                if (attempt == maxRetries - 1)
                {
                    _logger.LogError(ex, "Bedrock throttling exception after {Attempts} attempts", maxRetries);
                    throw;
                }

                // Exponential backoff with jitter
                var delay = baseDelayMs * (int)Math.Pow(2, attempt);
                var jitter = Random.Shared.Next(0, delay / 2);
                var totalDelay = delay + jitter;

                _logger.LogWarning(
                    "Bedrock throttled on attempt {Attempt}, retrying after {Delay}ms",
                    attempt + 1,
                    totalDelay);

                await Task.Delay(totalDelay, cancellationToken);
            }
            catch (Amazon.BedrockRuntime.Model.ModelTimeoutException ex)
            {
                _logger.LogError(ex, "Bedrock model timeout on attempt {Attempt}", attempt + 1);
                
                if (attempt == maxRetries - 1)
                {
                    throw;
                }

                await Task.Delay(baseDelayMs * (attempt + 1), cancellationToken);
            }
        }

        throw new InvalidOperationException("Failed to invoke Bedrock after maximum retries");
    }

    private string ExtractAnswerFromResponse(string responseJson)
    {
        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;

            // Amazon Nova response format: { "output": { "message": { "content": [{ "text": "..." }] } } }
            if (root.TryGetProperty("output", out var outputElement) &&
                outputElement.TryGetProperty("message", out var messageElement) &&
                messageElement.TryGetProperty("content", out var contentArray))
            {
                foreach (var contentItem in contentArray.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var textElement))
                    {
                        return textElement.GetString() ?? string.Empty;
                    }
                }
            }

            _logger.LogWarning("Could not extract text from Bedrock response, returning raw JSON");
            return responseJson;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing Bedrock response JSON");
            return "Error: Unable to parse AI response";
        }
    }

    private IEnumerable<KisanMitraAI.Core.Advisory.Citation> GenerateSyntheticCitations(string answer)
    {
        // Generate synthetic citations to maintain backward compatibility with client applications
        // that expect citations in the response structure
        var citations = new List<KisanMitraAI.Core.Advisory.Citation>
        {
            new KisanMitraAI.Core.Advisory.Citation(
                DocumentTitle: "AI-Generated Agricultural Advisory",
                DocumentUri: "bedrock://direct-invocation",
                RelevantExcerpt: answer.Length > 200 
                    ? answer.Substring(0, 200) + "..." 
                    : answer,
                RelevanceScore: 1.0f
            )
        };

        return citations;
    }

    private float CalculateConfidenceScore(string answer, string responseJson)
    {
        try
        {
            // Base confidence on response quality indicators
            float confidence = 0.5f; // Start with medium confidence

            // Check for stop reason (indicates complete response)
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;

            if (root.TryGetProperty("stopReason", out var stopReason))
            {
                var reason = stopReason.GetString();
                if (reason == "end_turn" || reason == "stop_sequence")
                {
                    confidence += 0.2f; // Complete response increases confidence
                }
            }

            // Longer, more detailed answers generally indicate higher confidence
            if (answer.Length > 500)
            {
                confidence += 0.1f;
            }

            // Check for structured content (bullet points, numbered lists)
            if (answer.Contains("1.") || answer.Contains("•") || answer.Contains("-"))
            {
                confidence += 0.1f;
            }

            // Check for specific agricultural terms (indicates domain relevance)
            var agriculturalTerms = new[] { "crop", "soil", "fertilizer", "irrigation", "pest", "yield", "harvest" };
            var termCount = agriculturalTerms.Count(term => 
                answer.Contains(term, StringComparison.OrdinalIgnoreCase));
            
            if (termCount >= 3)
            {
                confidence += 0.1f;
            }

            // Cap confidence at 1.0
            return Math.Min(1.0f, confidence);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error calculating confidence score, using default");
            return 0.7f; // Default medium-high confidence
        }
    }
}
