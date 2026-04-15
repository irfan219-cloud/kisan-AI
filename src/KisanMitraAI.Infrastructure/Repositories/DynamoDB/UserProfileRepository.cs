using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KisanMitraAI.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace KisanMitraAI.Infrastructure.Repositories.DynamoDB;

/// <summary>
/// Implementation of user profile repository using DynamoDB
/// </summary>
public class UserProfileRepository : IUserProfileRepository
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly DynamoDBConfiguration _config;
    private readonly ILogger<UserProfileRepository> _logger;

    public UserProfileRepository(
        IAmazonDynamoDB dynamoClient,
        IOptions<DynamoDBConfiguration> config,
        ILogger<UserProfileRepository> logger)
    {
        _dynamoClient = dynamoClient ?? throw new ArgumentNullException(nameof(dynamoClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SaveProfileAsync(
        FarmerProfile profile,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var item = new Dictionary<string, AttributeValue>
            {
                ["FarmerId"] = new AttributeValue { S = profile.FarmerId },
                ["Name"] = new AttributeValue { S = profile.Name },
                ["PhoneNumber"] = new AttributeValue { S = profile.PhoneNumber },
                ["PreferredLanguage"] = new AttributeValue { S = profile.PreferredLanguage.ToString() },
                ["PreferredDialect"] = new AttributeValue { S = profile.PreferredDialect?.ToString() ?? string.Empty },
                ["Location"] = new AttributeValue { S = profile.Location },
                ["Farms"] = new AttributeValue { S = JsonSerializer.Serialize(profile.Farms) },
                ["RegisteredAt"] = new AttributeValue { S = profile.RegisteredAt.ToString("o") },
                ["UpdatedAt"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("o") }
            };

            var request = new PutItemRequest
            {
                TableName = _config.UserProfileTableName,
                Item = item
            };

            await _dynamoClient.PutItemAsync(request, cancellationToken);
            
            _logger.LogInformation("Saved profile for farmer {FarmerId}", profile.FarmerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving profile for farmer {FarmerId}", profile.FarmerId);
            throw;
        }
    }

    public async Task<FarmerProfile?> GetProfileAsync(
        string farmerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetItemRequest
            {
                TableName = _config.UserProfileTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["FarmerId"] = new AttributeValue { S = farmerId }
                }
            };

            var response = await _dynamoClient.GetItemAsync(request, cancellationToken);

            if (!response.IsItemSet)
            {
                return null;
            }

            var item = response.Item;
            var farmsJson = item["Farms"].S;
            var farms = JsonSerializer.Deserialize<List<FarmProfile>>(farmsJson) ?? new List<FarmProfile>();

            var preferredLanguage = Enum.Parse<Language>(item["PreferredLanguage"].S);
            var preferredDialectStr = item["PreferredDialect"].S;
            var preferredDialect = string.IsNullOrEmpty(preferredDialectStr) 
                ? (Dialect?)null 
                : Enum.Parse<Dialect>(preferredDialectStr);

            return new FarmerProfile(
                farmerId: item["FarmerId"].S,
                name: item["Name"].S,
                phoneNumber: item["PhoneNumber"].S,
                preferredLanguage: preferredLanguage,
                preferredDialect: preferredDialect,
                location: item["Location"].S,
                farms: farms,
                registeredAt: DateTimeOffset.Parse(item["RegisteredAt"].S)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profile for farmer {FarmerId}", farmerId);
            throw;
        }
    }

    public async Task UpdatePreferencesAsync(
        string farmerId,
        string preferredLanguage,
        string preferredDialect,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new UpdateItemRequest
            {
                TableName = _config.UserProfileTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["FarmerId"] = new AttributeValue { S = farmerId }
                },
                UpdateExpression = "SET PreferredLanguage = :lang, PreferredDialect = :dialect, UpdatedAt = :updated",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":lang"] = new AttributeValue { S = preferredLanguage },
                    [":dialect"] = new AttributeValue { S = preferredDialect },
                    [":updated"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("o") }
                },
                ConditionExpression = "attribute_exists(FarmerId)"
            };

            await _dynamoClient.UpdateItemAsync(request, cancellationToken);
            
            _logger.LogInformation(
                "Updated preferences for farmer {FarmerId}: Language={Language}, Dialect={Dialect}", 
                farmerId, preferredLanguage, preferredDialect);
        }
        catch (ConditionalCheckFailedException)
        {
            _logger.LogWarning("Farmer {FarmerId} not found for preference update", farmerId);
            throw new InvalidOperationException($"Farmer {farmerId} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating preferences for farmer {FarmerId}", farmerId);
            throw;
        }
    }
}
