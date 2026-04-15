using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using KisanMitraAI.Core.VoiceIntelligence;
using KisanMitraAI.Core.VoiceIntelligence.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace KisanMitraAI.Infrastructure.AI;

/// <summary>
/// Implementation of query parser using Amazon Bedrock
/// </summary>
public class QueryParser : IQueryParser
{
    private readonly IAmazonBedrockRuntime _bedrockClient;
    private readonly ILogger<QueryParser> _logger;
    private readonly string _modelId = "us.amazon.nova-pro-v1:0"; // Nova Pro inference profile
    private readonly TimeSpan _parseTimeout = TimeSpan.FromSeconds(30);

    public QueryParser(
        IAmazonBedrockRuntime bedrockClient,
        ILogger<QueryParser> logger)
    {
        _bedrockClient = bedrockClient ?? throw new ArgumentNullException(nameof(bedrockClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ParsedQuery> ParseQueryAsync(
        string transcribedText,
        string context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transcribedText))
            throw new ArgumentException("Transcribed text cannot be null or empty", nameof(transcribedText));

        _logger.LogInformation("Parsing query: {Text}", transcribedText);

        try
        {
            using var timeoutCts = new CancellationTokenSource(_parseTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var prompt = BuildParsingPrompt(transcribedText, context);
            var requestBody = BuildBedrockRequest(prompt);

            var invokeRequest = new InvokeModelRequest
            {
                ModelId = _modelId,
                Body = new MemoryStream(Encoding.UTF8.GetBytes(requestBody)),
                ContentType = "application/json",
                Accept = "application/json"
            };

            var response = await _bedrockClient.InvokeModelAsync(invokeRequest, linkedCts.Token);

            using var responseStream = response.Body;
            var responseJson = await JsonDocument.ParseAsync(responseStream, cancellationToken: linkedCts.Token);

            var parsedQuery = ExtractParsedQuery(responseJson);

            _logger.LogInformation("Query parsed: Commodity={Commodity}, Location={Location}, RequiresClarification={RequiresClarification}",
                parsedQuery.Commodity, parsedQuery.Location, parsedQuery.RequiresClarification);

            return parsedQuery;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing query: {Text}", transcribedText);
            throw;
        }
    }

    private string BuildParsingPrompt(string transcribedText, string context)
    {
        return $@"You are an agricultural assistant helping farmers in India. Parse the following voice query to extract:
1. Commodity name (e.g., wheat, rice, tomato, onion)
2. Location (e.g., Delhi, Mumbai, Indore, Bhopal)
3. Intent (e.g., price_query, market_info)

If the query is ambiguous or missing information, indicate that clarification is needed.

Query: ""{transcribedText}""
Context: {context}

Respond in JSON format:
{{
  ""commodity"": ""extracted commodity or null"",
  ""location"": ""extracted location or null"",
  ""intent"": ""price_query"",
  ""requires_clarification"": true/false,
  ""clarification_prompt"": ""question to ask if clarification needed"",
  ""confidence"": 0.0-1.0
}}";
    }

    private string BuildBedrockRequest(string prompt)
    {
        var request = new
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
                max_new_tokens = 1000,
                temperature = 0.7
            }
        };

        return JsonSerializer.Serialize(request);
    }

    private ParsedQuery ExtractParsedQuery(JsonDocument responseJson)
    {
        // Extract content from Nova response
        var content = responseJson.RootElement
            .GetProperty("output")
            .GetProperty("message")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        // Parse the JSON response from the model
        // Look for JSON block in the response
        var jsonStart = content.IndexOf('{');
        var jsonEnd = content.LastIndexOf('}');

        if (jsonStart == -1 || jsonEnd == -1)
        {
            _logger.LogWarning("Could not find JSON in model response: {Content}", content);
            return new ParsedQuery(
                string.Empty,
                string.Empty,
                "unknown",
                true,
                "I couldn't understand your query. Could you please repeat?",
                0.0f);
        }

        var jsonContent = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
        using var parsedJson = JsonDocument.Parse(jsonContent);
        var root = parsedJson.RootElement;

        var commodity = root.TryGetProperty("commodity", out var commodityProp) && commodityProp.ValueKind != JsonValueKind.Null
            ? commodityProp.GetString() ?? string.Empty
            : string.Empty;

        var location = root.TryGetProperty("location", out var locationProp) && locationProp.ValueKind != JsonValueKind.Null
            ? locationProp.GetString() ?? string.Empty
            : string.Empty;

        var intent = root.TryGetProperty("intent", out var intentProp)
            ? intentProp.GetString() ?? "unknown"
            : "unknown";

        var requiresClarification = root.TryGetProperty("requires_clarification", out var clarificationProp)
            ? clarificationProp.GetBoolean()
            : string.IsNullOrEmpty(commodity) || string.IsNullOrEmpty(location);

        var clarificationPrompt = root.TryGetProperty("clarification_prompt", out var promptProp)
            ? promptProp.GetString()
            : null;

        var confidence = root.TryGetProperty("confidence", out var confidenceProp)
            ? (float)confidenceProp.GetDouble()
            : 0.5f;

        return new ParsedQuery(
            commodity,
            location,
            intent,
            requiresClarification,
            clarificationPrompt,
            confidence);
    }
}
