using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using KisanMitraAI.Infrastructure.Repositories.PostgreSQL;
using Microsoft.Extensions.Logging;

namespace KisanMitraAI.Infrastructure.Repositories.DynamoDB;

/// <summary>
/// Implementation of audit log repository using DynamoDB
/// </summary>
public class DynamoAuditLogRepository : IAuditLogRepository
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly ILogger<DynamoAuditLogRepository> _logger;
    private const string TableName = "AuditLogs";

    public DynamoAuditLogRepository(
        IAmazonDynamoDB dynamoClient,
        ILogger<DynamoAuditLogRepository> logger)
    {
        _dynamoClient = dynamoClient ?? throw new ArgumentNullException(nameof(dynamoClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task LogActionAsync(
        string farmerId,
        string action,
        string resourceType,
        string resourceId,
        string? details = null,
        string? ipAddress = null,
        string? userAgent = null,
        string status = "Success",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var timestamp = DateTimeOffset.UtcNow;
            var timestampMillis = timestamp.ToUnixTimeMilliseconds();

            var item = new Dictionary<string, AttributeValue>
            {
                ["FarmerId"] = new AttributeValue { S = farmerId },
                ["Timestamp"] = new AttributeValue { N = timestampMillis.ToString() },
                ["Action"] = new AttributeValue { S = action },
                ["ResourceType"] = new AttributeValue { S = resourceType },
                ["ResourceId"] = new AttributeValue { S = resourceId },
                ["Details"] = new AttributeValue { S = details ?? string.Empty },
                ["IpAddress"] = new AttributeValue { S = ipAddress ?? string.Empty },
                ["UserAgent"] = new AttributeValue { S = userAgent ?? string.Empty },
                ["Status"] = new AttributeValue { S = status }
            };

            var request = new PutItemRequest
            {
                TableName = TableName,
                Item = item
            };

            await _dynamoClient.PutItemAsync(request, cancellationToken);

            _logger.LogInformation(
                "Logged action {Action} for farmer {FarmerId} on {ResourceType}/{ResourceId}",
                action, farmerId, resourceType, resourceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging action {Action} for farmer {FarmerId}", action, farmerId);
            // Don't throw - audit logging should not break the main flow
        }
    }

    public async Task<IEnumerable<AuditLogEntry>> GetAuditTrailAsync(
        string farmerId,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        CancellationToken cancellationToken = default)
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
                },
                ScanIndexForward = false // Sort descending by timestamp
            };

            // Add timestamp range filter if provided
            if (startDate.HasValue || endDate.HasValue)
            {
                var filterExpressions = new List<string>();
                
                if (startDate.HasValue)
                {
                    var startMillis = startDate.Value.ToUnixTimeMilliseconds();
                    request.ExpressionAttributeValues[":startTime"] = new AttributeValue { N = startMillis.ToString() };
                    filterExpressions.Add("#ts >= :startTime");
                }

                if (endDate.HasValue)
                {
                    var endMillis = endDate.Value.ToUnixTimeMilliseconds();
                    request.ExpressionAttributeValues[":endTime"] = new AttributeValue { N = endMillis.ToString() };
                    filterExpressions.Add("#ts <= :endTime");
                }

                if (filterExpressions.Any())
                {
                    request.FilterExpression = string.Join(" AND ", filterExpressions);
                    request.ExpressionAttributeNames = new Dictionary<string, string>
                    {
                        ["#ts"] = "Timestamp"
                    };
                }
            }

            var response = await _dynamoClient.QueryAsync(request, cancellationToken);

            if (response.Items == null || response.Items.Count == 0)
            {
                return Enumerable.Empty<AuditLogEntry>();
            }

            return response.Items.Select(MapToModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit trail for farmer {FarmerId}", farmerId);
            throw;
        }
    }

    private AuditLogEntry MapToModel(Dictionary<string, AttributeValue> item)
    {
        var timestampMillis = long.Parse(item["Timestamp"].N);
        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestampMillis);

        return new AuditLogEntry(
            LogId: timestampMillis, // Use timestamp as LogId since DynamoDB doesn't auto-generate IDs
            FarmerId: item["FarmerId"].S,
            Action: item["Action"].S,
            ResourceType: item["ResourceType"].S,
            ResourceId: item["ResourceId"].S,
            Details: item["Details"].S,
            IpAddress: item["IpAddress"].S,
            UserAgent: item["UserAgent"].S,
            Timestamp: timestamp,
            Status: item["Status"].S
        );
    }
}
