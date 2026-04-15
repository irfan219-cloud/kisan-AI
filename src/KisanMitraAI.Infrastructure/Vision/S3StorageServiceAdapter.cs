using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;

namespace KisanMitraAI.Infrastructure.Vision;

/// <summary>
/// Adapter to make Storage.S3.S3StorageService compatible with Vision.IS3StorageService
/// </summary>
public class S3StorageServiceAdapter : IS3StorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3StorageServiceAdapter> _logger;
    private readonly string _bucketName;

    public string BucketName => _bucketName;

    public S3StorageServiceAdapter(
        IAmazonS3 s3Client,
        ILogger<S3StorageServiceAdapter> logger,
        string bucketName)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
    }

    public async Task<string> UploadAsync(
        Stream stream,
        string key,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
            };

            await _s3Client.PutObjectAsync(request, cancellationToken);
            _logger.LogInformation("Uploaded file to S3 with key {S3Key}", key);
            return key;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file with key {S3Key}", key);
            throw;
        }
    }

    public async Task<Stream> DownloadAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            var response = await _s3Client.GetObjectAsync(request, cancellationToken);
            _logger.LogInformation("Downloaded file from S3 with key {S3Key}", key);
            return response.ResponseStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file with key {S3Key}", key);
            throw;
        }
    }

    public async Task<string> GeneratePresignedUrlAsync(
        string key,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(expiration)
            };

            var url = await Task.Run(() => _s3Client.GetPreSignedURL(request), cancellationToken);
            _logger.LogInformation("Generated pre-signed URL for key {S3Key}", key);
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating pre-signed URL for key {S3Key}", key);
            throw;
        }
    }

    public async Task DeleteAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            await _s3Client.DeleteObjectAsync(request, cancellationToken);
            _logger.LogInformation("Deleted file from S3 with key {S3Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file with key {S3Key}", key);
            throw;
        }
    }
}
