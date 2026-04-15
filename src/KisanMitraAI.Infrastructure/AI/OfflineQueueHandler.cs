using Amazon.S3;
using KisanMitraAI.Core.VoiceIntelligence;
using KisanMitraAI.Infrastructure.Repositories.DynamoDB;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace KisanMitraAI.Infrastructure.AI;

/// <summary>
/// Implementation of offline queue handler for voice queries
/// </summary>
public class OfflineQueueHandler : IOfflineQueueHandler
{
    private readonly OfflineQueueRepository _queueRepository;
    private readonly IVoiceQueryHandler _voiceQueryHandler;
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<OfflineQueueHandler> _logger;
    private readonly string _bucketName;
    private const long MaxQueueSizeBytes = 50 * 1024 * 1024; // 50 MB

    public OfflineQueueHandler(
        OfflineQueueRepository queueRepository,
        IVoiceQueryHandler voiceQueryHandler,
        IAmazonS3 s3Client,
        ILogger<OfflineQueueHandler> logger,
        string bucketName)
    {
        _queueRepository = queueRepository ?? throw new ArgumentNullException(nameof(queueRepository));
        _voiceQueryHandler = voiceQueryHandler ?? throw new ArgumentNullException(nameof(voiceQueryHandler));
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
    }

    public async Task<string> QueueVoiceQueryAsync(
        string farmerId,
        string audioS3Key,
        string dialect,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(farmerId))
            throw new ArgumentException("Farmer ID cannot be null or empty", nameof(farmerId));
        if (string.IsNullOrWhiteSpace(audioS3Key))
            throw new ArgumentException("Audio S3 key cannot be null or empty", nameof(audioS3Key));
        if (string.IsNullOrWhiteSpace(dialect))
            throw new ArgumentException("Dialect cannot be null or empty", nameof(dialect));

        _logger.LogInformation("Queueing voice query for farmer {FarmerId}", farmerId);

        // Check queue depth before adding
        var currentDepth = await _queueRepository.GetQueueDepthAsync(farmerId, cancellationToken);
        if (currentDepth >= MaxQueueSizeBytes)
        {
            _logger.LogWarning("Queue size limit reached for farmer {FarmerId}", farmerId);
            throw new InvalidOperationException($"Queue size limit of {MaxQueueSizeBytes} bytes reached");
        }

        var operationData = new
        {
            Type = "VoiceQuery",
            AudioS3Key = audioS3Key,
            Dialect = dialect,
            QueuedAt = DateTimeOffset.UtcNow
        };

        var operationId = Guid.NewGuid().ToString();
        await _queueRepository.EnqueueOperationAsync(
            farmerId,
            "VoiceQuery",
            JsonSerializer.Serialize(operationData),
            cancellationToken);

        _logger.LogInformation("Voice query queued with ID {OperationId} for farmer {FarmerId}",
            operationId, farmerId);

        return operationId;
    }

    public async Task<int> ProcessQueuedQueriesAsync(
        string farmerId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(farmerId))
            throw new ArgumentException("Farmer ID cannot be null or empty", nameof(farmerId));

        _logger.LogInformation("Processing queued queries for farmer {FarmerId}", farmerId);

        var queuedOperations = await _queueRepository.DequeueOperationsAsync(
            farmerId,
            100, // Process up to 100 operations
            cancellationToken);

        var processedCount = 0;

        foreach (var operation in queuedOperations)
        {
            try
            {
                var operationData = JsonSerializer.Deserialize<QueuedVoiceQuery>(operation.OperationData);
                if (operationData == null)
                {
                    _logger.LogWarning("Could not deserialize operation data for operation {OperationId}",
                        operation.OperationId);
                    continue;
                }

                // Download audio from S3
                var audioResponse = await _s3Client.GetObjectAsync(_bucketName, operationData.AudioS3Key, cancellationToken);
                using var audioStream = audioResponse.ResponseStream;

                // Process the voice query
                await _voiceQueryHandler.ProcessVoiceQueryAsync(
                    audioStream,
                    operationData.Dialect,
                    farmerId,
                    cancellationToken);

                processedCount++;

                _logger.LogInformation("Processed queued voice query {OperationId} for farmer {FarmerId}",
                    operation.OperationId, farmerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing queued operation {OperationId} for farmer {FarmerId}",
                    operation.OperationId, farmerId);
                // Continue processing other operations
            }
        }

        _logger.LogInformation("Processed {Count} queued queries for farmer {FarmerId}",
            processedCount, farmerId);

        return processedCount;
    }

    public async Task<int> GetQueueDepthAsync(
        string farmerId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(farmerId))
            throw new ArgumentException("Farmer ID cannot be null or empty", nameof(farmerId));

        return await _queueRepository.GetQueueDepthAsync(farmerId, cancellationToken);
    }

    public async Task<bool> IsOfflineAsync()
    {
        // Simple connectivity check - try to access AWS service
        try
        {
            await _s3Client.ListBucketsAsync(CancellationToken.None);
            return false; // Online
        }
        catch
        {
            return true; // Offline
        }
    }

    private class QueuedVoiceQuery
    {
        public string Type { get; set; } = string.Empty;
        public string AudioS3Key { get; set; } = string.Empty;
        public string Dialect { get; set; } = string.Empty;
        public DateTimeOffset QueuedAt { get; set; }
    }
}
