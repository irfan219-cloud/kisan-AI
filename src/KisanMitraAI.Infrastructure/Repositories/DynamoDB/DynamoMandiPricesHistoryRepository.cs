using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KisanMitraAI.Core.Models;
using KisanMitraAI.Infrastructure.Repositories.Timestream;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KisanMitraAI.Infrastructure.Repositories.DynamoDB;

/// <summary>
/// DynamoDB implementation for Mandi prices history (30-day retention with TTL)
/// Cost-optimized replacement for Timestream
/// </summary>
public class DynamoMandiPricesHistoryRepository : IMandiPriceRepository
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly DynamoDBConfiguration _config;
    private readonly ILogger<DynamoMandiPricesHistoryRepository> _logger;

    public DynamoMandiPricesHistoryRepository(
        IAmazonDynamoDB dynamoClient,
        IOptions<DynamoDBConfiguration> config,
        ILogger<DynamoMandiPricesHistoryRepository> logger)
    {
        _dynamoClient = dynamoClient ?? throw new ArgumentNullException(nameof(dynamoClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StorePriceAsync(
        MandiPrice price,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var ttl = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
            var compositeKey = $"{price.Commodity}#{price.Location}";
            
            var item = new Dictionary<string, AttributeValue>
            {
                ["CommodityLocation"] = new AttributeValue { S = compositeKey },
                ["Timestamp"] = new AttributeValue { S = price.PriceDate.ToString("o") },
                ["Commodity"] = new AttributeValue { S = price.Commodity },
                ["Location"] = new AttributeValue { S = price.Location },
                ["MandiName"] = new AttributeValue { S = price.MandiName },
                ["MinPrice"] = new AttributeValue { N = price.MinPrice.ToString() },
                ["MaxPrice"] = new AttributeValue { N = price.MaxPrice.ToString() },
                ["ModalPrice"] = new AttributeValue { N = price.ModalPrice.ToString() },
                ["Unit"] = new AttributeValue { S = price.Unit },
                ["ExpiresAt"] = new AttributeValue { N = ttl.ToString() }
            };

            var request = new PutItemRequest
            {
                TableName = _config.MandiPricesHistoryTableName ?? "kisan-mitra-dev-data-storage-MandiPricesHistoryTable",
                Item = item
            };

            await _dynamoClient.PutItemAsync(request, cancellationToken);
            
            _logger.LogInformation(
                "Stored Mandi price for {Commodity} at {Location}", 
                price.Commodity, 
                price.Location);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing Mandi price for {Commodity}", price.Commodity);
            throw;
        }
    }

    public async Task<IEnumerable<MandiPrice>> GetCurrentPricesAsync(
        string commodity, 
        string location,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var compositeKey = $"{commodity}#{location}";

            _logger.LogInformation(
                "Querying current prices for {Commodity} in {Location} (key: {Key})",
                commodity,
                location,
                compositeKey);

            // Simplified query - just get latest prices without timestamp filter
            var request = new QueryRequest
            {
                TableName = _config.MandiPricesHistoryTableName ?? "kisan-mitra-dev-data-storage-MandiPricesHistoryTable",
                KeyConditionExpression = "CommodityLocation = :key",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":key"] = new AttributeValue { S = compositeKey }
                },
                ScanIndexForward = false,
                Limit = 10
            };

            var response = await _dynamoClient.QueryAsync(request, cancellationToken);
            
            _logger.LogInformation(
                "Query returned {Count} items for {Commodity} in {Location}",
                response.Items.Count,
                commodity,
                location);
            
            // TODO: Re-enable 24-hour filter once we have regular data ingestion
            // For now, return all available prices to avoid fallback
            var prices = response.Items
                .Select(ParseMandiPrice)
                .ToList();
            
            // COMMENTED OUT: 24-hour filter - will re-enable later
            // var cutoffTime = DateTimeOffset.UtcNow.AddHours(-24);
            // var prices = response.Items
            //     .Select(ParseMandiPrice)
            //     .Where(p => p.PriceDate >= cutoffTime)
            //     .ToList();
            
            _logger.LogInformation(
                "Returning {Count} prices (24-hour filter disabled for testing)",
                prices.Count);
            
            return prices;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Query timeout for {Commodity} prices - returning empty result", commodity);
            return Array.Empty<MandiPrice>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying current prices for {Commodity}", commodity);
            return Array.Empty<MandiPrice>();
        }
    }

    public async Task<IEnumerable<MandiPrice>> GetHistoricalPricesAsync(
        string commodity, 
        string location, 
        DateTimeOffset startDate, 
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var compositeKey = $"{commodity}#{location}";

            var request = new QueryRequest
            {
                TableName = _config.MandiPricesHistoryTableName ?? "kisan-mitra-dev-data-storage-MandiPricesHistoryTable",
                KeyConditionExpression = "CommodityLocation = :key AND #ts BETWEEN :start AND :end",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#ts"] = "Timestamp"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":key"] = new AttributeValue { S = compositeKey },
                    [":start"] = new AttributeValue { S = startDate.ToString("o") },
                    [":end"] = new AttributeValue { S = endDate.ToString("o") }
                },
                ScanIndexForward = true
            };

            var response = await _dynamoClient.QueryAsync(request, cancellationToken);
            return response.Items.Select(ParseMandiPrice).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying historical prices for {Commodity}", commodity);
            return Array.Empty<MandiPrice>();
        }
    }

    public async Task<IEnumerable<MandiPrice>> GetPriceTrendsAsync(
        string commodity, 
        string location,
        int daysBack = 30,
        CancellationToken cancellationToken = default)
    {
        var startDate = DateTimeOffset.UtcNow.AddDays(-daysBack);
        var endDate = DateTimeOffset.UtcNow;
        
        return await GetHistoricalPricesAsync(commodity, location, startDate, endDate, cancellationToken);
    }

    private MandiPrice ParseMandiPrice(Dictionary<string, AttributeValue> item)
    {
        return new MandiPrice(
            commodity: item["Commodity"].S,
            location: item["Location"].S,
            mandiName: item["MandiName"].S,
            minPrice: decimal.Parse(item["MinPrice"].N),
            maxPrice: decimal.Parse(item["MaxPrice"].N),
            modalPrice: decimal.Parse(item["ModalPrice"].N),
            priceDate: DateTimeOffset.Parse(item["Timestamp"].S),
            unit: item["Unit"].S
        );
    }
}
