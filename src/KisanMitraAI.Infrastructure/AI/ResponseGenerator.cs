using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using KisanMitraAI.Core.Models;
using KisanMitraAI.Core.VoiceIntelligence;
using KisanMitraAI.Core.VoiceIntelligence.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace KisanMitraAI.Infrastructure.AI;

/// <summary>
/// Implementation of response generator using Amazon Bedrock
/// </summary>
public class ResponseGenerator : IResponseGenerator
{
    private readonly IAmazonBedrockRuntime _bedrockClient;
    private readonly ILogger<ResponseGenerator> _logger;
    private readonly string _modelId = "us.amazon.nova-pro-v1:0"; // Nova Pro inference profile
    private readonly TimeSpan _generationTimeout = TimeSpan.FromSeconds(30);

    public ResponseGenerator(
        IAmazonBedrockRuntime bedrockClient,
        ILogger<ResponseGenerator> logger)
    {
        _bedrockClient = bedrockClient ?? throw new ArgumentNullException(nameof(bedrockClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> GenerateResponseAsync(
        ParsedQuery query,
        IEnumerable<MandiPrice> prices,
        string dialect,
        CancellationToken cancellationToken)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        if (prices == null)
            throw new ArgumentNullException(nameof(prices));
        if (string.IsNullOrWhiteSpace(dialect))
            throw new ArgumentException("Dialect cannot be null or empty", nameof(dialect));

        _logger.LogInformation("Generating response for {Commodity} in {Location} in dialect {Dialect}",
            query.Commodity, query.Location, dialect);

        try
        {
            using var timeoutCts = new CancellationTokenSource(_generationTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var prompt = BuildResponsePrompt(query, prices, dialect);
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

            var responseText = ExtractResponseText(responseJson);

            _logger.LogInformation("Generated response: {Response}", responseText);

            return responseText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating response for {Commodity} in {Location}",
                query.Commodity, query.Location);
            throw;
        }
    }

    private string BuildResponsePrompt(ParsedQuery query, IEnumerable<MandiPrice> prices, string dialect)
    {
        var priceList = prices.ToList();
        var priceInfo = new StringBuilder();

        if (priceList.Any())
        {
            foreach (var price in priceList)
            {
                priceInfo.AppendLine($"- {price.MandiName}: Min ₹{price.MinPrice}, Max ₹{price.MaxPrice}, Modal ₹{price.ModalPrice} per {price.Unit}");
            }
        }
        else
        {
            priceInfo.AppendLine("No prices available for this commodity and location.");
        }

        var dialectInstruction = GetDialectInstruction(dialect);

        return $@"You are an agricultural assistant helping farmers in India. Generate a natural, farmer-friendly response about Mandi prices.

Query: {query.Commodity} prices in {query.Location}

Current Prices:
{priceInfo}

{dialectInstruction}

Generate a concise, helpful response (2-3 sentences) that:
1. States the current modal price clearly
2. Mentions the price range if available
3. Is easy to understand for farmers
4. Uses simple language appropriate for voice delivery

Response:";
    }

    private string GetDialectInstruction(string dialect)
    {
        var dialectInstructions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Hindi", "Respond in simple Hindi that farmers can easily understand." },
            { "Bundelkhandi", "Respond in Hindi with Bundelkhandi dialect expressions where appropriate." },
            { "Bhojpuri", "Respond in Hindi with Bhojpuri dialect expressions where appropriate." },
            { "Marwari", "Respond in Hindi with Marwari dialect expressions where appropriate." },
            { "Tamil", "Respond in simple Tamil that farmers can easily understand." },
            { "Telugu", "Respond in simple Telugu that farmers can easily understand." },
            { "Bengali", "Respond in simple Bengali that farmers can easily understand." },
            { "Marathi", "Respond in simple Marathi that farmers can easily understand." }
        };

        return dialectInstructions.TryGetValue(dialect, out var instruction)
            ? instruction
            : "Respond in simple Hindi that farmers can easily understand.";
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
                max_new_tokens = 500,
                temperature = 0.7
            }
        };

        return JsonSerializer.Serialize(request);
    }

    private string ExtractResponseText(JsonDocument responseJson)
    {
        // Extract content from Nova response
        var content = responseJson.RootElement
            .GetProperty("output")
            .GetProperty("message")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        return content.Trim();
    }
}
