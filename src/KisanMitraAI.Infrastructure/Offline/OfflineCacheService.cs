using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KisanMitraAI.Core.Models;
using KisanMitraAI.Core.Offline;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace KisanMitraAI.Infrastructure.Offline;

/// <summary>
/// Implementation of offline cache service using DynamoDB
/// </summary>
public class OfflineCacheService : IOfflineCacheService
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly ILogger<OfflineCacheService> _logger;
    private readonly string _tableName;
    private const long MaxCacheSizeBytes = 50 * 1024 * 1024; // 50 MB
    private const int CacheRetentionDays = 7;

    public OfflineCacheService(
        IAmazonDynamoDB dynamoDb,
        ILogger<OfflineCacheService> logger,
        string tableName = "KisanMitra-OfflineCache")
    {
        _dynamoDb = dynamoDb ?? throw new ArgumentNullException(nameof(dynamoDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tableName = tableName;
    }

    public async Task CachePricesAsync(
        string farmerId,
        IEnumerable<MandiPrice> prices,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(farmerId))
            throw new ArgumentException("Farmer ID is required", nameof(farmerId));

        if (prices == null || !prices.Any())
        {
            _logger.LogWarning("No prices to cache for farmer {FarmerId}", farmerId);
            return;
        }

        try
        {
            // Check current cache size
            var currentSize = await GetCacheSizeAsync(farmerId, cancellationToken);
            
            // Filter prices to last 7 days
            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-CacheRetentionDays);
            var recentPrices = prices.Where(p => p.PriceDate >= cutoffDate).ToList();

            // Serialize prices
            var pricesJson = JsonSerializer.Serialize(recentPrices);
            var newDataSize = System.Text.Encoding.UTF8.GetByteCount(pricesJson);

            // Check if adding new data would exceed limit
            if (currentSize + newDataSize > MaxCacheSizeBytes)
            {
                _logger.LogWarning(
                    "Cache size limit would be exceeded for farmer {FarmerId}. Current: {CurrentSize}, New: {NewSize}, Limit: {Limit}",
                    farmerId, currentSize, newDataSize, MaxCacheSizeBytes);
                
                // Invalidate old cache to make room
                await InvalidateCacheAsync(farmerId, cancellationToken);
            }

            // Store in DynamoDB
            var request = new PutItemRequest
            {
                TableName = _tableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["FarmerId"] = new AttributeValue { S = farmerId },
                    ["CacheKey"] = new AttributeValue { S = "MandiPrices" },
                    ["Data"] = new AttributeValue { S = pricesJson },
                    ["CachedAt"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") },
                    ["ExpiresAt"] = new AttributeValue { N = DateTimeOffset.UtcNow.AddDays(CacheRetentionDays).ToUnixTimeSeconds().ToString() },
                    ["SizeBytes"] = new AttributeValue { N = newDataSize.ToString() }
                }
            };

            await _dynamoDb.PutItemAsync(request, cancellationToken);

            _logger.LogInformation(
                "Cached {Count} prices for farmer {FarmerId}, size: {Size} bytes",
                recentPrices.Count, farmerId, newDataSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching prices for farmer {FarmerId}", farmerId);
            throw;
        }
    }

    public async Task<IEnumerable<MandiPrice>> GetCachedPricesAsync(
        string farmerId,
        string? commodity = null,
        string? location = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(farmerId))
            throw new ArgumentException("Farmer ID is required", nameof(farmerId));

        try
        {
            var request = new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["FarmerId"] = new AttributeValue { S = farmerId },
                    ["CacheKey"] = new AttributeValue { S = "MandiPrices" }
                }
            };

            var response = await _dynamoDb.GetItemAsync(request, cancellationToken);

            if (!response.IsItemSet)
            {
                _logger.LogInformation("No cached prices found for farmer {FarmerId}", farmerId);
                return Enumerable.Empty<MandiPrice>();
            }

            // Check if cache has expired
            if (response.Item.TryGetValue("ExpiresAt", out var expiresAtAttr))
            {
                var expiresAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(expiresAtAttr.N));
                if (expiresAt < DateTimeOffset.UtcNow)
                {
                    _logger.LogInformation("Cached prices expired for farmer {FarmerId}", farmerId);
                    return Enumerable.Empty<MandiPrice>();
                }
            }

            var pricesJson = response.Item["Data"].S;
            var prices = JsonSerializer.Deserialize<List<MandiPrice>>(pricesJson) ?? new List<MandiPrice>();

            // Filter by commodity and location if specified
            var filteredPrices = prices.AsEnumerable();
            
            if (!string.IsNullOrWhiteSpace(commodity))
            {
                filteredPrices = filteredPrices.Where(p => 
                    p.Commodity.Equals(commodity, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                filteredPrices = filteredPrices.Where(p => 
                    p.Location.Equals(location, StringComparison.OrdinalIgnoreCase));
            }

            var result = filteredPrices.ToList();

            _logger.LogInformation(
                "Retrieved {Count} cached prices for farmer {FarmerId}",
                result.Count, farmerId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cached prices for farmer {FarmerId}", farmerId);
            throw;
        }
    }

    public async Task<long> GetCacheSizeAsync(
        string farmerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(farmerId))
            throw new ArgumentException("Farmer ID is required", nameof(farmerId));

        try
        {
            var request = new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "FarmerId = :farmerId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":farmerId"] = new AttributeValue { S = farmerId }
                },
                ProjectionExpression = "SizeBytes"
            };

            var response = await _dynamoDb.QueryAsync(request, cancellationToken);

            var totalSize = response.Items
                .Where(item => item.ContainsKey("SizeBytes"))
                .Sum(item => long.Parse(item["SizeBytes"].N));

            return totalSize;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache size for farmer {FarmerId}", farmerId);
            throw;
        }
    }

    public async Task InvalidateCacheAsync(
        string farmerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(farmerId))
            throw new ArgumentException("Farmer ID is required", nameof(farmerId));

        try
        {
            // Query all cache entries for farmer
            var queryRequest = new QueryRequest
            {
                TableName = _tableName,
                KeyConditionExpression = "FarmerId = :farmerId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":farmerId"] = new AttributeValue { S = farmerId }
                }
            };

            var queryResponse = await _dynamoDb.QueryAsync(queryRequest, cancellationToken);

            // Delete all entries
            foreach (var item in queryResponse.Items)
            {
                var deleteRequest = new DeleteItemRequest
                {
                    TableName = _tableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["FarmerId"] = item["FarmerId"],
                        ["CacheKey"] = item["CacheKey"]
                    }
                };

                await _dynamoDb.DeleteItemAsync(deleteRequest, cancellationToken);
            }

            _logger.LogInformation(
                "Invalidated {Count} cache entries for farmer {FarmerId}",
                queryResponse.Items.Count, farmerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache for farmer {FarmerId}", farmerId);
            throw;
        }
    }

    public async Task RefreshCacheAsync(
        string farmerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(farmerId))
            throw new ArgumentException("Farmer ID is required", nameof(farmerId));

        try
        {
            // This would typically fetch fresh prices from the price retriever
            // For now, we'll just log that a refresh was requested
            _logger.LogInformation("Cache refresh requested for farmer {FarmerId}", farmerId);
            
            // In a real implementation, this would:
            // 1. Fetch latest prices from MandiPriceRepository
            // 2. Call CachePricesAsync with the fresh data
            // 3. The old cache would be replaced automatically
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing cache for farmer {FarmerId}", farmerId);
            throw;
        }
    }
}
