using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KisanMitraAI.Infrastructure.Repositories.DynamoDB;

/// <summary>
/// Implementation of offline queue repository using DynamoDB
/// </summary>
public class OfflineQueueRepository : IOfflineQueueRepository
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly DynamoDBConfiguration _config;
    private readonly ILogger<OfflineQueueRepository> _logger;

    public OfflineQueueRepository(
        IAmazonDynamoDB dynamoClient,
        IOptions<DynamoDBConfiguration> config,
        ILogger<OfflineQueueRepository> logger)
    {
        _dynamoClient = dynamoClient ?? throw new ArgumentNullException(nameof(dynamoClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EnqueueOperationAsync(
        string farmerId,
        string operationType,
        string operationData,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var operationId = Guid.NewGuid().ToString();
            var queuedAt = DateTimeOffset.UtcNow;

            var item = new Dictionary<string, AttributeValue>
            {
                ["FarmerId"] = new AttributeValue { S = farmerId },
                ["OperationId"] = new AttributeValue { S = operationId },
                ["OperationType"] = new AttributeValue { S = operationType },
                ["OperationData"] = new AttributeValue { S = operationData },
                ["QueuedAt"] = new AttributeValue { S = queuedAt.ToString("o") },
                ["RetryCount"] = new AttributeValue { N = "0" },
                ["TTL"] = new AttributeValue { N = queuedAt.AddDays(7).ToUnixTimeSeconds().ToString() }
            };

            var request = new PutItemRequest
            {
                TableName = _config.OfflineQueueTableName,
                Item = item
            };

            await _dynamoClient.PutItemAsync(request, cancellationToken);
            
            _logger.LogInformation(
                "Enqueued operation {OperationId} of type {OperationType} for farmer {FarmerId}", 
                operationId, operationType, farmerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing operation for farmer {FarmerId}", farmerId);
            throw;
        }
    }

    public async Task<IEnumerable<QueuedOperation>> DequeueOperationsAsync(
        string farmerId,
        int maxItems = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new QueryRequest
            {
                TableName = _config.OfflineQueueTableName,
                KeyConditionExpression = "FarmerId = :farmerId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":farmerId"] = new AttributeValue { S = farmerId }
                },
                Limit = maxItems,
                ScanIndexForward = true // Oldest first
            };

            var response = await _dynamoClient.QueryAsync(request, cancellationToken);

            var operations = response.Items.Select(item => new QueuedOperation(
                OperationId: item["OperationId"].S,
                FarmerId: item["FarmerId"].S,
                OperationType: item["OperationType"].S,
                OperationData: item["OperationData"].S,
                QueuedAt: DateTimeOffset.Parse(item["QueuedAt"].S),
                RetryCount: int.Parse(item["RetryCount"].N)
            )).ToList();

            _logger.LogInformation(
                "Dequeued {Count} operations for farmer {FarmerId}", 
                operations.Count, farmerId);

            return operations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dequeuing operations for farmer {FarmerId}", farmerId);
            throw;
        }
    }

    public async Task<int> GetQueueDepthAsync(
        string farmerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new QueryRequest
            {
                TableName = _config.OfflineQueueTableName,
                KeyConditionExpression = "FarmerId = :farmerId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":farmerId"] = new AttributeValue { S = farmerId }
                },
                Select = Select.COUNT
            };

            var response = await _dynamoClient.QueryAsync(request, cancellationToken);
            return response.Count ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue depth for farmer {FarmerId}", farmerId);
            throw;
        }
    }

    public async Task DeleteOperationAsync(
        string farmerId,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DeleteItemRequest
            {
                TableName = _config.OfflineQueueTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["FarmerId"] = new AttributeValue { S = farmerId },
                    ["OperationId"] = new AttributeValue { S = operationId }
                }
            };

            await _dynamoClient.DeleteItemAsync(request, cancellationToken);
            
            _logger.LogInformation(
                "Deleted operation {OperationId} for farmer {FarmerId}", 
                operationId, farmerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting operation {OperationId}", operationId);
            throw;
        }
    }
}
