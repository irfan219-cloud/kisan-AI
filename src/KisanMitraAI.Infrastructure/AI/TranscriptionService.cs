using Amazon.TranscribeService;
using Amazon.TranscribeService.Model;
using Amazon.S3;
using Amazon.S3.Model;
using KisanMitraAI.Core.VoiceIntelligence;
using KisanMitraAI.Core.VoiceIntelligence.Models;
using Microsoft.Extensions.Logging;

namespace KisanMitraAI.Infrastructure.AI;

/// <summary>
/// Implementation of transcription service using Amazon Transcribe
/// </summary>
public class TranscriptionService : ITranscriptionService
{
    private readonly IAmazonTranscribeService _transcribeClient;
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<TranscriptionService> _logger;
    private readonly string _bucketName;
    private readonly TimeSpan _transcriptionTimeout = TimeSpan.FromSeconds(120); // 2 minutes for transcription

    public TranscriptionService(
        IAmazonTranscribeService transcribeClient,
        IAmazonS3 s3Client,
        ILogger<TranscriptionService> logger,
        string bucketName)
    {
        _transcribeClient = transcribeClient ?? throw new ArgumentNullException(nameof(transcribeClient));
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        string languageCode,
        CancellationToken cancellationToken)
    {
        if (audioStream == null)
            throw new ArgumentNullException(nameof(audioStream));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("Language code cannot be null or empty", nameof(languageCode));

        var startTime = DateTimeOffset.UtcNow;
        var jobName = $"transcription-{Guid.NewGuid()}";
        var s3Key = $"audio/{jobName}.mp3";

        try
        {
            // Upload audio to S3
            _logger.LogInformation("Uploading audio to S3: {S3Key}", s3Key);
            await _s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = s3Key,
                InputStream = audioStream
            }, cancellationToken);

            // Start transcription job
            _logger.LogInformation("Starting transcription job: {JobName}", jobName);
            var startJobRequest = new StartTranscriptionJobRequest
            {
                TranscriptionJobName = jobName,
                LanguageCode = languageCode,
                Media = new Media
                {
                    MediaFileUri = $"s3://{_bucketName}/{s3Key}"
                },
                MediaFormat = MediaFormat.Mp3
            };

            await _transcribeClient.StartTranscriptionJobAsync(startJobRequest, cancellationToken);

            // Poll for completion with timeout
            using var timeoutCts = new CancellationTokenSource(_transcriptionTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            TranscriptionJob? job = null;
            while (!linkedCts.Token.IsCancellationRequested)
            {
                await Task.Delay(2000, linkedCts.Token); // Poll every 2 seconds

                var getJobResponse = await _transcribeClient.GetTranscriptionJobAsync(
                    new GetTranscriptionJobRequest { TranscriptionJobName = jobName },
                    linkedCts.Token);

                job = getJobResponse.TranscriptionJob;

                if (job.TranscriptionJobStatus == TranscriptionJobStatus.COMPLETED)
                {
                    _logger.LogInformation("Transcription completed: {JobName}", jobName);
                    break;
                }

                if (job.TranscriptionJobStatus == TranscriptionJobStatus.FAILED)
                {
                    _logger.LogError("Transcription failed: {JobName}, Reason: {FailureReason}",
                        jobName, job.FailureReason);
                    throw new InvalidOperationException($"Transcription failed: {job.FailureReason}");
                }
            }

            if (job == null || job.TranscriptionJobStatus != TranscriptionJobStatus.COMPLETED)
            {
                throw new TimeoutException("Transcription did not complete within the timeout period");
            }

            // Get transcription result
            var transcriptUri = job.Transcript.TranscriptFileUri;
            using var httpClient = new HttpClient();
            var transcriptJson = await httpClient.GetStringAsync(transcriptUri, cancellationToken);

            // Parse transcript (simplified - in production, use proper JSON parsing)
            var transcribedText = ExtractTranscriptText(transcriptJson);
            var confidence = 0.95f; // Placeholder - extract from actual transcript

            return new TranscriptionResult(
                transcribedText,
                confidence,
                languageCode,
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transcribing audio: {JobName}", jobName);
            throw;
        }
        finally
        {
            // Cleanup: Delete transcription job and S3 object
            try
            {
                await _transcribeClient.DeleteTranscriptionJobAsync(
                    new DeleteTranscriptionJobRequest { TranscriptionJobName = jobName },
                    CancellationToken.None);

                await _s3Client.DeleteObjectAsync(
                    new DeleteObjectRequest { BucketName = _bucketName, Key = s3Key },
                    CancellationToken.None);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Error cleaning up transcription resources: {JobName}", jobName);
            }
        }
    }

    private string ExtractTranscriptText(string transcriptJson)
    {
        // Simplified extraction - in production, use System.Text.Json
        // The transcript JSON has structure: { "results": { "transcripts": [{ "transcript": "text" }] } }
        var startIndex = transcriptJson.IndexOf("\"transcript\":\"", StringComparison.Ordinal);
        if (startIndex == -1)
            return string.Empty;

        startIndex += "\"transcript\":\"".Length;
        var endIndex = transcriptJson.IndexOf("\"", startIndex, StringComparison.Ordinal);
        if (endIndex == -1)
            return string.Empty;

        return transcriptJson.Substring(startIndex, endIndex - startIndex);
    }
}
