using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KisanMitraAI.Core.Models;
using KisanMitraAI.Infrastructure.Repositories.PostgreSQL;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace KisanMitraAI.Infrastructure.Repositories.DynamoDB;

/// <summary>
/// Implementation of farm repository using DynamoDB
/// </summary>
public class DynamoDBFarmRepository : IFarmRepository
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly ILogger<DynamoDBFarmRepository> _logger;
    private const string TableName = "FarmProfiles";

    public DynamoDBFarmRepository(
        IAmazonDynamoDB dynamoClient,
        ILogger<DynamoDBFarmRepository> logger)
    {
        _dynamoClient = dynamoClient ?? throw new ArgumentNullException(nameof(dynamoClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> CreateAsync(FarmProfile farm, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = new Dictionary<string, AttributeValue>
            {
                ["FarmerId"] = new AttributeValue { S = farm.FarmerId },
                ["FarmId"] = new AttributeValue { S = farm.FarmId },
                ["AreaInAcres"] = new AttributeValue { N = farm.AreaInAcres.ToString() },
                ["SoilType"] = new AttributeValue { S = farm.SoilType },
                ["IrrigationType"] = new AttributeValue { S = farm.IrrigationType },
                ["CurrentCrops"] = new AttributeValue { S = JsonSerializer.Serialize(farm.CurrentCrops) },
                ["Latitude"] = new AttributeValue { N = farm.Coordinates.Latitude.ToString() },
                ["Longitude"] = new AttributeValue { N = farm.Coordinates.Longitude.ToString() },
                ["CreatedAt"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("o") },
                ["UpdatedAt"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("o") }
            };

            var request = new PutItemRequest
            {
                TableName = TableName,
                Item = item
            };

            await _dynamoClient.PutItemAsync(request, cancellationToken);

            _logger.LogInformation("Created farm {FarmId} for farmer {FarmerId}", farm.FarmId, farm.FarmerId);
            return farm.FarmId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating farm {FarmId} for farmer {FarmerId}", farm.FarmId, farm.FarmerId);
            throw;
        }
    }

    public async Task<FarmProfile?> GetByIdAsync(string farmId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Note: We need FarmerId to efficiently query, but the interface only provides FarmId
            // We'll use Scan as a fallback, which is less efficient but necessary for this interface
            var request = new ScanRequest
            {
                TableName = TableName,
                FilterExpression = "FarmId = :farmId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":farmId"] = new AttributeValue { S = farmId }
                }
            };

            var response = await _dynamoClient.ScanAsync(request, cancellationToken);

            if (response.Items == null || response.Items.Count == 0)
            {
                return null;
            }

            return MapToModel(response.Items[0]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting farm {FarmId}", farmId);
            throw;
        }
    }

    public async Task<IEnumerable<FarmProfile>> GetByFarmerIdAsync(string farmerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new QueryRequest
            {
                TableName = TableName,
                KeyConditionExpression = "FarmerId = :farmerId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":farmerId"] = new AttributeValue { S = farmerId }
                }
            };

            var response = await _dynamoClient.QueryAsync(request, cancellationToken);

            if (response.Items == null || response.Items.Count == 0)
            {
                return Enumerable.Empty<FarmProfile>();
            }

            return response.Items.Select(MapToModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting farms for farmer {FarmerId}", farmerId);
            throw;
        }
    }

    public async Task UpdateAsync(FarmProfile farm, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new UpdateItemRequest
            {
                TableName = TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["FarmerId"] = new AttributeValue { S = farm.FarmerId },
                    ["FarmId"] = new AttributeValue { S = farm.FarmId }
                },
                UpdateExpression = "SET AreaInAcres = :area, SoilType = :soil, IrrigationType = :irrigation, " +
                                   "CurrentCrops = :crops, Latitude = :lat, Longitude = :lon, UpdatedAt = :updated",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":area"] = new AttributeValue { N = farm.AreaInAcres.ToString() },
                    [":soil"] = new AttributeValue { S = farm.SoilType },
                    [":irrigation"] = new AttributeValue { S = farm.IrrigationType },
                    [":crops"] = new AttributeValue { S = JsonSerializer.Serialize(farm.CurrentCrops) },
                    [":lat"] = new AttributeValue { N = farm.Coordinates.Latitude.ToString() },
                    [":lon"] = new AttributeValue { N = farm.Coordinates.Longitude.ToString() },
                    [":updated"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("o") }
                },
                ConditionExpression = "attribute_exists(FarmerId) AND attribute_exists(FarmId)"
            };

            await _dynamoClient.UpdateItemAsync(request, cancellationToken);

            _logger.LogInformation("Updated farm {FarmId} for farmer {FarmerId}", farm.FarmId, farm.FarmerId);
        }
        catch (ConditionalCheckFailedException)
        {
            _logger.LogWarning("Farm {FarmId} not found for update", farm.FarmId);
            throw new InvalidOperationException($"Farm {farm.FarmId} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating farm {FarmId}", farm.FarmId);
            throw;
        }
    }

    public async Task DeleteAsync(string farmId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Note: We need FarmerId to efficiently delete, but the interface only provides FarmId
            // First, we need to find the item to get the FarmerId
            var farm = await GetByIdAsync(farmId, cancellationToken);
            
            if (farm == null)
            {
                _logger.LogInformation("Farm {FarmId} not found for deletion", farmId);
                return;
            }

            var request = new DeleteItemRequest
            {
                TableName = TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["FarmerId"] = new AttributeValue { S = farm.FarmerId },
                    ["FarmId"] = new AttributeValue { S = farmId }
                }
            };

            await _dynamoClient.DeleteItemAsync(request, cancellationToken);

            _logger.LogInformation("Deleted farm {FarmId}", farmId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting farm {FarmId}", farmId);
            throw;
        }
    }

    private FarmProfile MapToModel(Dictionary<string, AttributeValue> item)
    {
        var cropsJson = item["CurrentCrops"].S;
        var crops = JsonSerializer.Deserialize<List<string>>(cropsJson) ?? new List<string>();
        
        var latitude = double.Parse(item["Latitude"].N);
        var longitude = double.Parse(item["Longitude"].N);
        var coordinates = new GeoCoordinates(latitude, longitude);

        return new FarmProfile(
            farmId: item["FarmId"].S,
            farmerId: item["FarmerId"].S,
            areaInAcres: float.Parse(item["AreaInAcres"].N),
            soilType: item["SoilType"].S,
            irrigationType: item["IrrigationType"].S,
            currentCrops: crops,
            coordinates: coordinates
        );
    }
}
