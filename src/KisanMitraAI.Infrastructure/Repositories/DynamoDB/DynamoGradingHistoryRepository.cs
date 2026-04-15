using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KisanMitraAI.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace KisanMitraAI.Infrastructure.Repositories.DynamoDB;

/// <summary>
/// DynamoDB implementation for grading history (30-day retention with TTL)
/// Cost-optimized replacement for Timestream
/// </summary>
public class DynamoGradingHistoryRepository : Timestream.IGradingHistoryRepository
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly DynamoDBConfiguration _config;
    private readonly ILogger<DynamoGradingHistoryRepository> _logger;

    public DynamoGradingHistoryRepository(
        IAmazonDynamoDB dynamoClient,
        IOptions<DynamoDBConfiguration> config,
        ILogger<DynamoGradingHistoryRepository> logger)
    {
        _dynamoClient = dynamoClient ?? throw new ArgumentNullException(nameof(dynamoClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StoreGradingAsync(
        GradingRecord record,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var ttl = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
            
            var item = new Dictionary<string, AttributeValue>
            {
                ["FarmerId"] = new AttributeValue { S = record.FarmerId },
                ["GradingDate"] = new AttributeValue { S = record.Timestamp.ToString("o") },
                ["RecordId"] = new AttributeValue { S = record.RecordId },
                ["ProduceType"] = new AttributeValue { S = record.ProduceType },
                ["Grade"] = new AttributeValue { S = record.Grade.ToString() },
                ["CertifiedPrice"] = new AttributeValue { N = record.CertifiedPrice.ToString() },
                ["ImageS3Key"] = new AttributeValue { S = record.ImageS3Key },
                ["Analysis"] = new AttributeValue { S = JsonSerializer.Serialize(record.Analysis) },
                ["ExpiresAt"] = new AttributeValue { N = ttl.ToString() }
            };

            var request = new PutItemRequest
            {
                TableName = _config.GradingHistoryTableName ?? "kisan-mitra-dev-data-storage-GradingHistoryTable",
                Item = item
            };

            await _dynamoClient.PutItemAsync(request, cancellationToken);
            
            _logger.LogInformation(
                "Stored grading record {RecordId} for farmer {FarmerId}", 
                record.RecordId, 
                record.FarmerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing grading record {RecordId}", record.RecordId);
            throw;
        }
    }

    public async Task<IEnumerable<GradingRecord>> GetGradingHistoryAsync(
        string farmerId,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var start = startDate ?? DateTimeOffset.UtcNow.AddDays(-30);
            var end = endDate ?? DateTimeOffset.UtcNow;

            var request = new QueryRequest
            {
                TableName = _config.GradingHistoryTableName ?? "kisan-mitra-dev-data-storage-GradingHistoryTable",
                KeyConditionExpression = "FarmerId = :farmerId AND GradingDate BETWEEN :start AND :end",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":farmerId"] = new AttributeValue { S = farmerId },
                    [":start"] = new AttributeValue { S = start.ToString("o") },
                    [":end"] = new AttributeValue { S = end.ToString("o") }
                },
                ScanIndexForward = false
            };

            var response = await _dynamoClient.QueryAsync(request, cancellationToken);
            var records = new List<GradingRecord>();

            foreach (var item in response.Items)
            {
                records.Add(ParseGradingRecord(item));
            }

            return records;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying grading history for farmer {FarmerId}", farmerId);
            return Array.Empty<GradingRecord>();
        }
    }

    private GradingRecord ParseGradingRecord(Dictionary<string, AttributeValue> item)
    {
        var analysisJson = item["Analysis"].S;
        ImageAnalysisResult analysis;
        
        if (string.IsNullOrEmpty(analysisJson))
        {
            analysis = CreateDefaultAnalysis();
        }
        else
        {
            try
            {
                // Try to deserialize with custom options for backward compatibility
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                // First, deserialize to a dynamic object to check structure
                var jsonDoc = JsonDocument.Parse(analysisJson);
                var root = jsonDoc.RootElement;
                
                // Check if it's old format (has uniformity/ripeness) or new format (has ColorUniformity/Brightness)
                if (root.TryGetProperty("colorProfile", out var colorProfileElement))
                {
                    bool isOldFormat = colorProfileElement.TryGetProperty("uniformity", out _);
                    
                    if (isOldFormat)
                    {
                        // Convert old format to new format
                        analysis = ConvertOldFormatAnalysis(root);
                    }
                    else
                    {
                        // New format - deserialize normally
                        analysis = JsonSerializer.Deserialize<ImageAnalysisResult>(analysisJson, options) 
                            ?? CreateDefaultAnalysis();
                    }
                }
                else
                {
                    analysis = CreateDefaultAnalysis();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse analysis JSON, using default. JSON: {Json}", analysisJson);
                analysis = CreateDefaultAnalysis();
            }
        }

        return new GradingRecord(
            recordId: item["RecordId"].S,
            farmerId: item["FarmerId"].S,
            produceType: item["ProduceType"].S,
            grade: Enum.Parse<QualityGrade>(item["Grade"].S),
            certifiedPrice: decimal.Parse(item["CertifiedPrice"].N),
            imageS3Key: item["ImageS3Key"].S,
            timestamp: DateTimeOffset.Parse(item["GradingDate"].S),
            analysis: analysis
        );
    }

    private ImageAnalysisResult ConvertOldFormatAnalysis(JsonElement root)
    {
        // Extract values from old format
        var averageSize = root.TryGetProperty("averageSize", out var sizeEl) 
            ? sizeEl.GetSingle() : 0f;
        var confidenceScore = root.TryGetProperty("confidenceScore", out var confEl) 
            ? confEl.GetSingle() * 100 : 80f; // Old format was 0-1, new is 0-100
        
        // Convert old colorProfile format
        var colorProfile = root.TryGetProperty("colorProfile", out var cpEl)
            ? new ColorProfile(
                dominantColor: cpEl.TryGetProperty("dominantColor", out var dcEl) 
                    ? dcEl.GetString() ?? "Unknown" : "Unknown",
                colorUniformity: cpEl.TryGetProperty("uniformity", out var uEl) 
                    ? uEl.GetSingle() * 100 : 50f, // Old format was 0-1, new is 0-100
                brightness: cpEl.TryGetProperty("ripeness", out var rEl) 
                    ? rEl.GetSingle() * 100 : 50f) // Use ripeness as brightness approximation
            : new ColorProfile("Unknown", 50f, 50f);
        
        // Extract defects (if any)
        var defects = new List<Defect>();
        if (root.TryGetProperty("defects", out var defectsEl) && defectsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var defectEl in defectsEl.EnumerateArray())
            {
                // Old format defects - skip for now as structure might be different
            }
        }
        
        return new ImageAnalysisResult(averageSize, colorProfile, defects, confidenceScore);
    }

    private ImageAnalysisResult CreateDefaultAnalysis()
    {
        return new ImageAnalysisResult(
            averageSize: 0f,
            colorProfile: new ColorProfile("Unknown", 0f, 0f),
            defects: Array.Empty<Defect>(),
            confidenceScore: 0f
        );
    }
}
