using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KisanMitraAI.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace KisanMitraAI.Infrastructure.Repositories.DynamoDB;

/// <summary>
/// DynamoDB implementation for soil data history (30-day retention with TTL)
/// Cost-optimized replacement for Timestream
/// </summary>
public class DynamoSoilDataHistoryRepository : Timestream.ISoilDataRepository
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly DynamoDBConfiguration _config;
    private readonly ILogger<DynamoSoilDataHistoryRepository> _logger;

    public DynamoSoilDataHistoryRepository(
        IAmazonDynamoDB dynamoClient,
        IOptions<DynamoDBConfiguration> config,
        ILogger<DynamoSoilDataHistoryRepository> logger)
    {
        _dynamoClient = dynamoClient ?? throw new ArgumentNullException(nameof(dynamoClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StoreSoilDataAsync(
        SoilHealthData soilData,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var ttl = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
            
            var item = new Dictionary<string, AttributeValue>
            {
                ["FarmerId"] = new AttributeValue { S = soilData.FarmerId },
                ["TestDate"] = new AttributeValue { S = soilData.TestDate.ToString("o") },
                ["Location"] = new AttributeValue { S = soilData.Location },
                ["LabId"] = new AttributeValue { S = soilData.LabId },
                ["Nitrogen"] = new AttributeValue { N = soilData.Nitrogen.ToString() },
                ["Phosphorus"] = new AttributeValue { N = soilData.Phosphorus.ToString() },
                ["Potassium"] = new AttributeValue { N = soilData.Potassium.ToString() },
                ["pH"] = new AttributeValue { N = soilData.pH.ToString() },
                ["OrganicCarbon"] = new AttributeValue { N = soilData.OrganicCarbon.ToString() },
                ["Sulfur"] = new AttributeValue { N = soilData.Sulfur.ToString() },
                ["Zinc"] = new AttributeValue { N = soilData.Zinc.ToString() },
                ["Boron"] = new AttributeValue { N = soilData.Boron.ToString() },
                ["Iron"] = new AttributeValue { N = soilData.Iron.ToString() },
                ["Manganese"] = new AttributeValue { N = soilData.Manganese.ToString() },
                ["Copper"] = new AttributeValue { N = soilData.Copper.ToString() },
                ["ExpiresAt"] = new AttributeValue { N = ttl.ToString() }
            };

            var request = new PutItemRequest
            {
                TableName = _config.SoilDataHistoryTableName ?? "kisan-mitra-dev-data-storage-SoilDataHistoryTable",
                Item = item
            };

            await _dynamoClient.PutItemAsync(request, cancellationToken);
            
            _logger.LogInformation(
                "Stored soil data for farmer {FarmerId} at location {Location}", 
                soilData.FarmerId, 
                soilData.Location);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing soil data for farmer {FarmerId}", soilData.FarmerId);
            throw;
        }
    }

    public async Task<IEnumerable<SoilHealthData>> GetSoilHistoryAsync(
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
                TableName = _config.SoilDataHistoryTableName ?? "kisan-mitra-dev-data-storage-SoilDataHistoryTable",
                KeyConditionExpression = "FarmerId = :farmerId AND TestDate BETWEEN :start AND :end",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":farmerId"] = new AttributeValue { S = farmerId },
                    [":start"] = new AttributeValue { S = start.ToString("o") },
                    [":end"] = new AttributeValue { S = end.ToString("o") }
                },
                ScanIndexForward = false // Descending order (newest first)
            };

            var response = await _dynamoClient.QueryAsync(request, cancellationToken);
            var soilDataList = new List<SoilHealthData>();

            foreach (var item in response.Items)
            {
                soilDataList.Add(ParseSoilData(item));
            }

            return soilDataList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying soil history for farmer {FarmerId}", farmerId);
            return Array.Empty<SoilHealthData>();
        }
    }

    private SoilHealthData ParseSoilData(Dictionary<string, AttributeValue> item)
    {
        return new SoilHealthData(
            farmerId: item["FarmerId"].S,
            location: item["Location"].S,
            nitrogen: float.Parse(item["Nitrogen"].N),
            phosphorus: float.Parse(item["Phosphorus"].N),
            potassium: float.Parse(item["Potassium"].N),
            pH: float.Parse(item["pH"].N),
            organicCarbon: float.Parse(item["OrganicCarbon"].N),
            sulfur: float.Parse(item["Sulfur"].N),
            zinc: float.Parse(item["Zinc"].N),
            boron: float.Parse(item["Boron"].N),
            iron: float.Parse(item["Iron"].N),
            manganese: float.Parse(item["Manganese"].N),
            copper: float.Parse(item["Copper"].N),
            testDate: DateTimeOffset.Parse(item["TestDate"].S),
            labId: item["LabId"].S
        );
    }
}
