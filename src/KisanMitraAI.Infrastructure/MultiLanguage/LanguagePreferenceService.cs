namespace KisanMitraAI.Infrastructure.MultiLanguage;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KisanMitraAI.Core.Models;
using KisanMitraAI.Core.MultiLanguage;
using Microsoft.Extensions.Logging;

/// <summary>
/// DynamoDB-based implementation of language preference service
/// </summary>
public class LanguagePreferenceService : ILanguagePreferenceService
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly ILogger<LanguagePreferenceService> _logger;
    private const string TableName = "KisanMitra-LanguagePreferences";

    public LanguagePreferenceService(
        IAmazonDynamoDB dynamoDb,
        ILogger<LanguagePreferenceService> logger)
    {
        _dynamoDb = dynamoDb ?? throw new ArgumentNullException(nameof(dynamoDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SavePreferenceAsync(
        string farmerId,
        Language language,
        Dialect? dialect,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(farmerId))
            throw new ArgumentException("Farmer ID cannot be null or empty", nameof(farmerId));

        _logger.LogInformation(
            "Saving language preference for farmer {FarmerId}: Language={Language}, Dialect={Dialect}",
            farmerId, language, dialect);

        var now = DateTimeOffset.UtcNow;
        var item = new Dictionary<string, AttributeValue>
        {
            ["FarmerId"] = new AttributeValue { S = farmerId },
            ["Language"] = new AttributeValue { S = language.ToString() },
            ["CreatedAt"] = new AttributeValue { S = now.ToString("O") },
            ["UpdatedAt"] = new AttributeValue { S = now.ToString("O") }
        };

        if (dialect.HasValue)
        {
            item["Dialect"] = new AttributeValue { S = dialect.Value.ToString() };
        }

        var request = new PutItemRequest
        {
            TableName = TableName,
            Item = item
        };

        try
        {
            await _dynamoDb.PutItemAsync(request, cancellationToken);
            _logger.LogInformation("Successfully saved language preference for farmer {FarmerId}", farmerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save language preference for farmer {FarmerId}", farmerId);
            throw;
        }
    }

    public async Task<LanguagePreference?> GetPreferenceAsync(
        string farmerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(farmerId))
            throw new ArgumentException("Farmer ID cannot be null or empty", nameof(farmerId));

        _logger.LogInformation("Retrieving language preference for farmer {FarmerId}", farmerId);

        var request = new GetItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["FarmerId"] = new AttributeValue { S = farmerId }
            }
        };

        try
        {
            var response = await _dynamoDb.GetItemAsync(request, cancellationToken);

            if (response.Item == null || response.Item.Count == 0)
            {
                _logger.LogInformation("No language preference found for farmer {FarmerId}", farmerId);
                return null;
            }

            var language = Enum.Parse<Language>(response.Item["Language"].S);
            Dialect? dialect = response.Item.ContainsKey("Dialect")
                ? Enum.Parse<Dialect>(response.Item["Dialect"].S)
                : null;
            var createdAt = DateTimeOffset.Parse(response.Item["CreatedAt"].S);
            var updatedAt = DateTimeOffset.Parse(response.Item["UpdatedAt"].S);

            var preference = new LanguagePreference(
                farmerId,
                language,
                dialect,
                createdAt,
                updatedAt);

            _logger.LogInformation(
                "Retrieved language preference for farmer {FarmerId}: Language={Language}, Dialect={Dialect}",
                farmerId, language, dialect);

            return preference;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve language preference for farmer {FarmerId}", farmerId);
            throw;
        }
    }
}
