using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KisanMitraAI.Core.Models;
using KisanMitraAI.Core.VoiceIntelligence;
using KisanMitraAI.Core.VoiceIntelligence.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace KisanMitraAI.Infrastructure.Repositories.DynamoDB;

/// <summary>
/// DynamoDB implementation for voice query history
/// Stores query metadata with S3 keys (not presigned URLs)
/// </summary>
public class DynamoVoiceQueryHistoryRepository : IVoiceQueryHistoryRepository
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly DynamoDBConfiguration _config;
    private readonly ILogger<DynamoVoiceQueryHistoryRepository> _logger;
    private const string TABLE_NAME = "kisan-mitra-VoiceQueryHistory";

    public DynamoVoiceQueryHistoryRepository(
        IAmazonDynamoDB dynamoClient,
        IOptions<DynamoDBConfiguration> config,
        ILogger<DynamoVoiceQueryHistoryRepository> logger)
    {
        _dynamoClient = dynamoClient ?? throw new ArgumentNullException(nameof(dynamoClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SaveQueryAsync(
        VoiceQueryHistoryItem item,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var ttl = DateTimeOffset.UtcNow.AddDays(90).ToUnixTimeSeconds(); // 90-day retention
            
            var dynamoItem = new Dictionary<string, AttributeValue>
            {
                ["FarmerId"] = new AttributeValue { S = item.FarmerId },
                ["Timestamp"] = new AttributeValue { S = item.Timestamp.ToString("o") },
                ["QueryId"] = new AttributeValue { S = item.QueryId },
                ["Transcription"] = new AttributeValue { S = item.Transcription },
                ["ResponseText"] = new AttributeValue { S = item.ResponseText },
                ["Dialect"] = new AttributeValue { S = item.Dialect },
                ["Confidence"] = new AttributeValue { N = item.Confidence.ToString() },
                ["AudioS3Key"] = new AttributeValue { S = item.AudioS3Key },
                ["ResponseAudioS3Key"] = new AttributeValue { S = item.ResponseAudioS3Key },
                ["IsFavorite"] = new AttributeValue { BOOL = item.IsFavorite },
                ["Prices"] = new AttributeValue { S = JsonSerializer.Serialize(item.Prices) },
                ["ExpiresAt"] = new AttributeValue { N = ttl.ToString() }
            };

            var request = new PutItemRequest
            {
                TableName = TABLE_NAME,
                Item = dynamoItem
            };

            await _dynamoClient.PutItemAsync(request, cancellationToken);
            
            _logger.LogInformation(
                "Saved voice query {QueryId} for farmer {FarmerId}", 
                item.QueryId, 
                item.FarmerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving voice query {QueryId}", item.QueryId);
            throw;
        }
    }

    public async Task<IEnumerable<VoiceQueryHistoryItem>> GetHistoryAsync(
        string farmerId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new QueryRequest
            {
                TableName = TABLE_NAME,
                KeyConditionExpression = "FarmerId = :farmerId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":farmerId"] = new AttributeValue { S = farmerId }
                },
                ScanIndexForward = false, // Most recent first
                Limit = limit
            };

            var response = await _dynamoClient.QueryAsync(request, cancellationToken);
            var items = new List<VoiceQueryHistoryItem>();

            foreach (var item in response.Items)
            {
                items.Add(ParseHistoryItem(item));
            }

            _logger.LogInformation(
                "Retrieved {Count} voice queries for farmer {FarmerId}", 
                items.Count, 
                farmerId);

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving voice query history for farmer {FarmerId}", farmerId);
            return Array.Empty<VoiceQueryHistoryItem>();
        }
    }

    public async Task<IEnumerable<VoiceQueryHistoryItem>> GetFavoritesAsync(
        string farmerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new QueryRequest
            {
                TableName = TABLE_NAME,
                IndexName = "FavoriteIndex", // GSI on FarmerId + IsFavorite
                KeyConditionExpression = "FarmerId = :farmerId",
                FilterExpression = "IsFavorite = :true",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":farmerId"] = new AttributeValue { S = farmerId },
                    [":true"] = new AttributeValue { BOOL = true }
                },
                ScanIndexForward = false
            };

            var response = await _dynamoClient.QueryAsync(request, cancellationToken);
            var items = new List<VoiceQueryHistoryItem>();

            foreach (var item in response.Items)
            {
                items.Add(ParseHistoryItem(item));
            }

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving favorite queries for farmer {FarmerId}", farmerId);
            return Array.Empty<VoiceQueryHistoryItem>();
        }
    }

    public async Task ToggleFavoriteAsync(
        string farmerId,
        string queryId,
        bool isFavorite,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // First, get the item to find its timestamp
            var query = await GetQueryByIdAsync(farmerId, queryId, cancellationToken);
            if (query == null)
            {
                _logger.LogWarning("Query {QueryId} not found for farmer {FarmerId}", queryId, farmerId);
                return;
            }

            var request = new UpdateItemRequest
            {
                TableName = TABLE_NAME,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["FarmerId"] = new AttributeValue { S = farmerId },
                    ["Timestamp"] = new AttributeValue { S = query.Timestamp.ToString("o") }
                },
                UpdateExpression = "SET IsFavorite = :isFavorite",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":isFavorite"] = new AttributeValue { BOOL = isFavorite }
                }
            };

            await _dynamoClient.UpdateItemAsync(request, cancellationToken);
            
            _logger.LogInformation(
                "Toggled favorite status for query {QueryId} to {IsFavorite}", 
                queryId, 
                isFavorite);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling favorite for query {QueryId}", queryId);
            throw;
        }
    }

    public async Task DeleteQueryAsync(
        string farmerId,
        string queryId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // First, get the item to find its timestamp
            var query = await GetQueryByIdAsync(farmerId, queryId, cancellationToken);
            if (query == null)
            {
                _logger.LogWarning("Query {QueryId} not found for farmer {FarmerId}", queryId, farmerId);
                return;
            }

            var request = new DeleteItemRequest
            {
                TableName = TABLE_NAME,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["FarmerId"] = new AttributeValue { S = farmerId },
                    ["Timestamp"] = new AttributeValue { S = query.Timestamp.ToString("o") }
                }
            };

            await _dynamoClient.DeleteItemAsync(request, cancellationToken);
            
            _logger.LogInformation("Deleted query {QueryId} for farmer {FarmerId}", queryId, farmerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting query {QueryId}", queryId);
            throw;
        }
    }

    public async Task<VoiceQueryHistoryItem?> GetQueryByIdAsync(
        string farmerId,
        string queryId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new QueryRequest
            {
                TableName = TABLE_NAME,
                IndexName = "QueryIdIndex", // GSI on QueryId
                KeyConditionExpression = "QueryId = :queryId",
                FilterExpression = "FarmerId = :farmerId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":queryId"] = new AttributeValue { S = queryId },
                    [":farmerId"] = new AttributeValue { S = farmerId }
                },
                Limit = 1
            };

            var response = await _dynamoClient.QueryAsync(request, cancellationToken);
            
            if (response.Items.Count == 0)
            {
                return null;
            }

            return ParseHistoryItem(response.Items[0]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving query {QueryId}", queryId);
            return null;
        }
    }

    private VoiceQueryHistoryItem ParseHistoryItem(Dictionary<string, AttributeValue> item)
    {
        var pricesJson = item.ContainsKey("Prices") ? item["Prices"].S : "[]";
        var prices = string.IsNullOrEmpty(pricesJson) 
            ? Array.Empty<MandiPrice>() 
            : JsonSerializer.Deserialize<IEnumerable<MandiPrice>>(pricesJson) ?? Array.Empty<MandiPrice>();

        return new VoiceQueryHistoryItem
        {
            QueryId = item["QueryId"].S,
            FarmerId = item["FarmerId"].S,
            Transcription = item["Transcription"].S,
            ResponseText = item["ResponseText"].S,
            Dialect = item["Dialect"].S,
            Confidence = double.Parse(item["Confidence"].N),
            AudioS3Key = item["AudioS3Key"].S,
            ResponseAudioS3Key = item["ResponseAudioS3Key"].S,
            Timestamp = DateTimeOffset.Parse(item["Timestamp"].S),
            IsFavorite = item.ContainsKey("IsFavorite") && item["IsFavorite"].BOOL == true,
            Prices = prices
        };
    }
}
