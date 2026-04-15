using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace KisanMitraAI.Infrastructure.Repositories.DynamoDB;

/// <summary>
/// Implementation of session repository using DynamoDB
/// </summary>
public class SessionRepository : ISessionRepository
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly DynamoDBConfiguration _config;
    private readonly ILogger<SessionRepository> _logger;

    public SessionRepository(
        IAmazonDynamoDB dynamoClient,
        IOptions<DynamoDBConfiguration> config,
        ILogger<SessionRepository> logger)
    {
        _dynamoClient = dynamoClient ?? throw new ArgumentNullException(nameof(dynamoClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SaveSessionAsync(
        string farmerId,
        string sessionId,
        List<ConversationExchange> exchanges,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Limit to max exchanges
            var limitedExchanges = exchanges.TakeLast(_config.MaxExchangesPerSession).ToList();

            var now = DateTimeOffset.UtcNow;
            var item = new Dictionary<string, AttributeValue>
            {
                ["FarmerId"] = new AttributeValue { S = farmerId },
                ["SessionId"] = new AttributeValue { S = sessionId },
                ["Exchanges"] = new AttributeValue { S = JsonSerializer.Serialize(limitedExchanges) },
                ["CreatedAt"] = new AttributeValue { S = now.ToString("o") },
                ["LastUpdatedAt"] = new AttributeValue { S = now.ToString("o") },
                ["TTL"] = new AttributeValue { N = now.AddHours(_config.SessionExpirationHours).ToUnixTimeSeconds().ToString() }
            };

            var request = new PutItemRequest
            {
                TableName = _config.SessionTableName,
                Item = item
            };

            await _dynamoClient.PutItemAsync(request, cancellationToken);
            
            _logger.LogInformation(
                "Saved session {SessionId} with {Count} exchanges for farmer {FarmerId}", 
                sessionId, limitedExchanges.Count, farmerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving session {SessionId} for farmer {FarmerId}", sessionId, farmerId);
            throw;
        }
    }

    public async Task<ConversationSession?> GetSessionAsync(
        string farmerId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetItemRequest
            {
                TableName = _config.SessionTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["FarmerId"] = new AttributeValue { S = farmerId },
                    ["SessionId"] = new AttributeValue { S = sessionId }
                }
            };

            var response = await _dynamoClient.GetItemAsync(request, cancellationToken);

            if (!response.IsItemSet)
            {
                return null;
            }

            var item = response.Item;
            var exchangesJson = item["Exchanges"].S;
            var exchanges = JsonSerializer.Deserialize<List<ConversationExchange>>(exchangesJson) 
                ?? new List<ConversationExchange>();

            return new ConversationSession(
                SessionId: item["SessionId"].S,
                FarmerId: item["FarmerId"].S,
                Exchanges: exchanges,
                CreatedAt: DateTimeOffset.Parse(item["CreatedAt"].S),
                LastUpdatedAt: DateTimeOffset.Parse(item["LastUpdatedAt"].S)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session {SessionId} for farmer {FarmerId}", sessionId, farmerId);
            throw;
        }
    }

    public async Task<ConversationSession?> GetSessionAsync(
        string farmerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Query for the most recent session for this farmer
            var request = new QueryRequest
            {
                TableName = _config.SessionTableName,
                KeyConditionExpression = "FarmerId = :farmerId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":farmerId"] = new AttributeValue { S = farmerId }
                },
                ScanIndexForward = false, // Sort descending by sort key
                Limit = 1
            };

            var response = await _dynamoClient.QueryAsync(request, cancellationToken);

            if (response.Items == null || response.Items.Count == 0)
            {
                return null;
            }

            var item = response.Items[0];
            var exchangesJson = item["Exchanges"].S;
            var exchanges = JsonSerializer.Deserialize<List<ConversationExchange>>(exchangesJson) 
                ?? new List<ConversationExchange>();

            return new ConversationSession(
                SessionId: item["SessionId"].S,
                FarmerId: item["FarmerId"].S,
                Exchanges: exchanges,
                CreatedAt: DateTimeOffset.Parse(item["CreatedAt"].S),
                LastUpdatedAt: DateTimeOffset.Parse(item["LastUpdatedAt"].S)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest session for farmer {FarmerId}", farmerId);
            throw;
        }
    }

    public async Task AddExchangeAsync(
        string farmerId,
        string sessionId,
        ConversationExchange exchange,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get existing session
            var session = await GetSessionAsync(farmerId, sessionId, cancellationToken);
            
            if (session == null)
            {
                // Create new session
                await SaveSessionAsync(farmerId, sessionId, new List<ConversationExchange> { exchange }, cancellationToken);
                return;
            }

            // Add exchange and limit to max
            var exchanges = session.Exchanges.ToList();
            exchanges.Add(exchange);
            var limitedExchanges = exchanges.TakeLast(_config.MaxExchangesPerSession).ToList();

            // Update session
            var request = new UpdateItemRequest
            {
                TableName = _config.SessionTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["FarmerId"] = new AttributeValue { S = farmerId },
                    ["SessionId"] = new AttributeValue { S = sessionId }
                },
                UpdateExpression = "SET Exchanges = :exchanges, LastUpdatedAt = :updated, #ttl = :ttl",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#ttl"] = "TTL"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":exchanges"] = new AttributeValue { S = JsonSerializer.Serialize(limitedExchanges) },
                    [":updated"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("o") },
                    [":ttl"] = new AttributeValue { N = DateTimeOffset.UtcNow.AddHours(_config.SessionExpirationHours).ToUnixTimeSeconds().ToString() }
                }
            };

            await _dynamoClient.UpdateItemAsync(request, cancellationToken);
            
            _logger.LogInformation(
                "Added exchange to session {SessionId} for farmer {FarmerId}", 
                sessionId, farmerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding exchange to session {SessionId}", sessionId);
            throw;
        }
    }
}
