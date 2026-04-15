using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.TimestreamWrite;
using Amazon.TimestreamWrite.Model;
using KisanMitraAI.Core.Security;
using KisanMitraAI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KisanMitraAI.Infrastructure.Security;

/// <summary>
/// Implementation of data deletion service for GDPR compliance
/// </summary>
public class DataDeletionService : IDataDeletionService
{
    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly IAmazonTimestreamWrite _timestreamClient;
    private readonly KisanMitraDbContext _dbContext;
    private readonly ILogger<DataDeletionService> _logger;
    private readonly string _bucketName;
    private readonly string _timestreamDatabase;

    public DataDeletionService(
        IAmazonS3 s3Client,
        IAmazonDynamoDB dynamoDbClient,
        IAmazonTimestreamWrite timestreamClient,
        KisanMitraDbContext dbContext,
        IConfiguration configuration,
        ILogger<DataDeletionService> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _dynamoDbClient = dynamoDbClient ?? throw new ArgumentNullException(nameof(dynamoDbClient));
        _timestreamClient = timestreamClient ?? throw new ArgumentNullException(nameof(timestreamClient));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _bucketName = configuration["AWS:S3:BucketName"] 
            ?? throw new InvalidOperationException("S3 bucket name not configured");
        _timestreamDatabase = configuration["AWS:Timestream:DatabaseName"] 
            ?? throw new InvalidOperationException("Timestream database not configured");
    }

    public async Task<DataDeletionReport> DeleteFarmerDataAsync(
        string farmerId, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(farmerId))
        {
            throw new ArgumentException("Farmer ID cannot be null or empty", nameof(farmerId));
        }

        _logger.LogInformation("Starting data deletion for farmer {FarmerId}", farmerId);

        var deletedCounts = new Dictionary<string, int>();
        var preservedAuditLogs = new List<string>();
        var errors = new List<string>();

        try
        {
            // 1. Delete from S3 (images, audio, documents)
            var s3Count = await DeleteFromS3Async(farmerId, cancellationToken);
            deletedCounts["S3_Files"] = s3Count;

            // 2. Delete from DynamoDB (user profiles, sessions, offline queue, farms, plans)
            var dynamoCount = await DeleteFromDynamoDBAsync(farmerId, cancellationToken);
            deletedCounts["DynamoDB_Records"] = dynamoCount;

            // 3. Delete from Timestream (soil data, prices, grades)
            // Note: Timestream doesn't support DELETE, so we mark records as deleted
            var timestreamCount = await MarkTimestreamRecordsDeletedAsync(farmerId, cancellationToken);
            deletedCounts["Timestream_Records"] = timestreamCount;

            // 5. Preserve audit logs (legally required)
            var auditLogs = await PreserveAuditLogsAsync(farmerId, cancellationToken);
            preservedAuditLogs.AddRange(auditLogs);

            _logger.LogInformation(
                "Data deletion completed for farmer {FarmerId}. Deleted: {DeletedCounts}",
                farmerId,
                deletedCounts);

            return new DataDeletionReport(
                FarmerId: farmerId,
                DeletedAt: DateTimeOffset.UtcNow,
                DeletedRecordCounts: deletedCounts,
                PreservedAuditLogs: preservedAuditLogs,
                IsComplete: true,
                ErrorMessage: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting data for farmer {FarmerId}", farmerId);
            
            return new DataDeletionReport(
                FarmerId: farmerId,
                DeletedAt: DateTimeOffset.UtcNow,
                DeletedRecordCounts: deletedCounts,
                PreservedAuditLogs: preservedAuditLogs,
                IsComplete: false,
                ErrorMessage: ex.Message);
        }
    }

    public async Task<string> ScheduleDeletionAsync(
        string farmerId, 
        string requestedBy, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(farmerId))
        {
            throw new ArgumentException("Farmer ID cannot be null or empty", nameof(farmerId));
        }

        var requestId = Guid.NewGuid().ToString();
        var scheduledFor = DateTimeOffset.UtcNow.AddDays(30); // 30-day grace period

        _logger.LogInformation(
            "Scheduling data deletion for farmer {FarmerId}, request {RequestId}, scheduled for {ScheduledFor}",
            farmerId,
            requestId,
            scheduledFor);

        // Store deletion request in DynamoDB
        var request = new PutItemRequest
        {
            TableName = "DeletionRequests",
            Item = new Dictionary<string, AttributeValue>
            {
                ["RequestId"] = new AttributeValue { S = requestId },
                ["FarmerId"] = new AttributeValue { S = farmerId },
                ["RequestedBy"] = new AttributeValue { S = requestedBy },
                ["Status"] = new AttributeValue { S = DeletionStatus.Scheduled.ToString() },
                ["RequestedAt"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") },
                ["ScheduledFor"] = new AttributeValue { S = scheduledFor.ToString("O") }
            }
        };

        await _dynamoDbClient.PutItemAsync(request, cancellationToken);

        return requestId;
    }

    public async Task<DeletionRequestStatus> GetDeletionStatusAsync(
        string requestId, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(requestId))
        {
            throw new ArgumentException("Request ID cannot be null or empty", nameof(requestId));
        }

        var request = new GetItemRequest
        {
            TableName = "DeletionRequests",
            Key = new Dictionary<string, AttributeValue>
            {
                ["RequestId"] = new AttributeValue { S = requestId }
            }
        };

        var response = await _dynamoDbClient.GetItemAsync(request, cancellationToken);

        if (!response.IsItemSet)
        {
            throw new InvalidOperationException($"Deletion request {requestId} not found");
        }

        var item = response.Item;

        return new DeletionRequestStatus(
            RequestId: requestId,
            FarmerId: item["FarmerId"].S,
            Status: Enum.Parse<DeletionStatus>(item["Status"].S),
            RequestedAt: DateTimeOffset.Parse(item["RequestedAt"].S),
            ScheduledFor: item.ContainsKey("ScheduledFor") ? DateTimeOffset.Parse(item["ScheduledFor"].S) : null,
            CompletedAt: item.ContainsKey("CompletedAt") ? DateTimeOffset.Parse(item["CompletedAt"].S) : null,
            ErrorMessage: item.ContainsKey("ErrorMessage") ? item["ErrorMessage"].S : null);
    }

    private async Task<int> DeleteFromS3Async(string farmerId, CancellationToken cancellationToken)
    {
        var count = 0;
        var prefix = $"farmers/{farmerId}/";

        try
        {
            var listRequest = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix
            };

            ListObjectsV2Response listResponse;
            do
            {
                listResponse = await _s3Client.ListObjectsV2Async(listRequest, cancellationToken);

                if (listResponse.S3Objects.Count > 0)
                {
                    var deleteRequest = new DeleteObjectsRequest
                    {
                        BucketName = _bucketName,
                        Objects = listResponse.S3Objects.Select(o => new KeyVersion { Key = o.Key }).ToList()
                    };

                    var deleteResponse = await _s3Client.DeleteObjectsAsync(deleteRequest, cancellationToken);
                    count += deleteResponse.DeletedObjects.Count;
                }

                listRequest.ContinuationToken = listResponse.NextContinuationToken;
            }
            while (listResponse.IsTruncated == true);

            _logger.LogInformation("Deleted {Count} files from S3 for farmer {FarmerId}", count, farmerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting S3 files for farmer {FarmerId}", farmerId);
            throw;
        }

        return count;
    }

    private async Task<int> DeleteFromDynamoDBAsync(string farmerId, CancellationToken cancellationToken)
    {
        var count = 0;
        var tables = new[] { "UserProfiles", "Sessions", "OfflineQueue", "FarmProfiles", "RegenerativePlans" };

        foreach (var table in tables)
        {
            try
            {
                // Query items for this farmer
                var queryRequest = new QueryRequest
                {
                    TableName = table,
                    KeyConditionExpression = "FarmerId = :farmerId",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":farmerId"] = new AttributeValue { S = farmerId }
                    }
                };

                var queryResponse = await _dynamoDbClient.QueryAsync(queryRequest, cancellationToken);

                // Delete each item
                foreach (var item in queryResponse.Items)
                {
                    var deleteRequest = new DeleteItemRequest
                    {
                        TableName = table,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            ["FarmerId"] = item["FarmerId"]
                        }
                    };

                    // Add sort key based on table
                    if (item.ContainsKey("Timestamp"))
                    {
                        deleteRequest.Key["Timestamp"] = item["Timestamp"];
                    }
                    else if (table == "FarmProfiles" && item.ContainsKey("FarmId"))
                    {
                        deleteRequest.Key["FarmId"] = item["FarmId"];
                    }
                    else if (table == "RegenerativePlans" && item.ContainsKey("PlanId"))
                    {
                        deleteRequest.Key["PlanId"] = item["PlanId"];
                    }

                    await _dynamoDbClient.DeleteItemAsync(deleteRequest, cancellationToken);
                    count++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting from DynamoDB table {Table} for farmer {FarmerId}", table, farmerId);
                throw;
            }
        }

        _logger.LogInformation("Deleted {Count} records from DynamoDB for farmer {FarmerId}", count, farmerId);
        return count;
    }

    private async Task<int> MarkTimestreamRecordsDeletedAsync(string farmerId, CancellationToken cancellationToken)
    {
        // Timestream doesn't support DELETE operations
        // Instead, we write tombstone records to mark data as deleted
        var count = 0;

        try
        {
            var tables = new[] { "SoilData", "MandiPrices", "GradingHistory" };

            foreach (var table in tables)
            {
                var records = new List<Record>
                {
                    new Record
                    {
                        Dimensions = new List<Dimension>
                        {
                            new Dimension { Name = "FarmerId", Value = farmerId },
                            new Dimension { Name = "RecordType", Value = "DELETED" }
                        },
                        MeasureName = "deletion_marker",
                        MeasureValue = "1",
                        MeasureValueType = MeasureValueType.BIGINT,
                        Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                        TimeUnit = TimeUnit.MILLISECONDS
                    }
                };

                var writeRequest = new WriteRecordsRequest
                {
                    DatabaseName = _timestreamDatabase,
                    TableName = table,
                    Records = records
                };

                await _timestreamClient.WriteRecordsAsync(writeRequest, cancellationToken);
                count++;
            }

            _logger.LogInformation("Marked {Count} Timestream tables as deleted for farmer {FarmerId}", count, farmerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking Timestream records as deleted for farmer {FarmerId}", farmerId);
            throw;
        }

        return count;
    }

    private async Task<List<string>> PreserveAuditLogsAsync(string farmerId, CancellationToken cancellationToken)
    {
        var preservedLogs = new List<string>();

        try
        {
            var auditLogs = await _dbContext.AuditLogs
                .Where(a => a.FarmerId == farmerId)
                .Select(a => a.LogId.ToString())
                .ToListAsync(cancellationToken);

            preservedLogs.AddRange(auditLogs);

            _logger.LogInformation(
                "Preserved {Count} audit logs for farmer {FarmerId} (legally required)",
                auditLogs.Count,
                farmerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preserving audit logs for farmer {FarmerId}", farmerId);
        }

        return preservedLogs;
    }
}
