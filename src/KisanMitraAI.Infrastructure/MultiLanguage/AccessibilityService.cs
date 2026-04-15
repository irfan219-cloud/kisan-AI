namespace KisanMitraAI.Infrastructure.MultiLanguage;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KisanMitraAI.Core.MultiLanguage;
using Microsoft.Extensions.Logging;

/// <summary>
/// DynamoDB-based implementation of accessibility service
/// </summary>
public class AccessibilityService : IAccessibilityService
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly ILogger<AccessibilityService> _logger;
    private const string TableName = "KisanMitra-AccessibilitySettings";

    public AccessibilityService(
        IAmazonDynamoDB dynamoDb,
        ILogger<AccessibilityService> logger)
    {
        _dynamoDb = dynamoDb ?? throw new ArgumentNullException(nameof(dynamoDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AccessibilitySettings> GetSettingsAsync(
        string farmerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(farmerId))
            throw new ArgumentException("Farmer ID cannot be null or empty", nameof(farmerId));

        _logger.LogInformation("Retrieving accessibility settings for farmer {FarmerId}", farmerId);

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
                _logger.LogInformation("No accessibility settings found for farmer {FarmerId}, returning defaults", farmerId);
                return GetDefaultSettings();
            }

            var settings = new AccessibilitySettings(
                HighContrastMode: bool.Parse(response.Item["HighContrastMode"].S),
                TextSize: Enum.Parse<TextSize>(response.Item["TextSize"].S),
                ScreenReaderEnabled: bool.Parse(response.Item["ScreenReaderEnabled"].S),
                KeyboardNavigationEnabled: bool.Parse(response.Item["KeyboardNavigationEnabled"].S),
                UpdatedAt: DateTimeOffset.Parse(response.Item["UpdatedAt"].S));

            _logger.LogInformation("Retrieved accessibility settings for farmer {FarmerId}", farmerId);
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve accessibility settings for farmer {FarmerId}", farmerId);
            throw;
        }
    }

    public async Task UpdateSettingsAsync(
        string farmerId,
        AccessibilitySettings settings,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(farmerId))
            throw new ArgumentException("Farmer ID cannot be null or empty", nameof(farmerId));
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        _logger.LogInformation("Updating accessibility settings for farmer {FarmerId}", farmerId);

        var now = DateTimeOffset.UtcNow;
        var item = new Dictionary<string, AttributeValue>
        {
            ["FarmerId"] = new AttributeValue { S = farmerId },
            ["HighContrastMode"] = new AttributeValue { S = settings.HighContrastMode.ToString() },
            ["TextSize"] = new AttributeValue { S = settings.TextSize.ToString() },
            ["ScreenReaderEnabled"] = new AttributeValue { S = settings.ScreenReaderEnabled.ToString() },
            ["KeyboardNavigationEnabled"] = new AttributeValue { S = settings.KeyboardNavigationEnabled.ToString() },
            ["UpdatedAt"] = new AttributeValue { S = now.ToString("O") }
        };

        var request = new PutItemRequest
        {
            TableName = TableName,
            Item = item
        };

        try
        {
            await _dynamoDb.PutItemAsync(request, cancellationToken);
            _logger.LogInformation("Successfully updated accessibility settings for farmer {FarmerId}", farmerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update accessibility settings for farmer {FarmerId}", farmerId);
            throw;
        }
    }

    private AccessibilitySettings GetDefaultSettings()
    {
        return new AccessibilitySettings(
            HighContrastMode: false,
            TextSize: TextSize.Normal,
            ScreenReaderEnabled: false,
            KeyboardNavigationEnabled: true,
            UpdatedAt: DateTimeOffset.UtcNow);
    }
}
