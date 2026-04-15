using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KisanMitraAI.Infrastructure.Storage.S3;

/// <summary>
/// Implementation of S3 storage service with encryption and access control
/// </summary>
public class S3StorageService : IS3StorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly S3Configuration _config;
    private readonly ILogger<S3StorageService> _logger;

    public S3StorageService(
        IAmazonS3 s3Client,
        IOptions<S3Configuration> config,
        ILogger<S3StorageService> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> UploadAsync(
        Stream fileStream, 
        string fileName, 
        string farmerId,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var s3Key = GenerateS3Key(fileName, farmerId, contentType);

            var request = new PutObjectRequest
            {
                BucketName = _config.BucketName,
                Key = s3Key,
                InputStream = fileStream,
                ContentType = contentType,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
                Metadata =
                {
                    ["farmer-id"] = farmerId,
                    ["upload-date"] = DateTimeOffset.UtcNow.ToString("o")
                }
            };

            await _s3Client.PutObjectAsync(request, cancellationToken);
            
            _logger.LogInformation(
                "Uploaded file {FileName} to S3 with key {S3Key} for farmer {FarmerId}", 
                fileName, s3Key, farmerId);

            return s3Key;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName} for farmer {FarmerId}", fileName, farmerId);
            throw;
        }
    }

    public async Task<Stream> DownloadAsync(
        string s3Key,
        string farmerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = _config.BucketName,
                Key = s3Key
            };

            var response = await _s3Client.GetObjectAsync(request, cancellationToken);

            // Verify farmer ID matches for access control
            if (response.Metadata["x-amz-meta-farmer-id"] != farmerId)
            {
                _logger.LogWarning(
                    "Access denied: Farmer {FarmerId} attempted to access file {S3Key} owned by another farmer",
                    farmerId, s3Key);
                throw new UnauthorizedAccessException("Access denied to this file");
            }

            _logger.LogInformation(
                "Downloaded file {S3Key} for farmer {FarmerId}", 
                s3Key, farmerId);

            return response.ResponseStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {S3Key} for farmer {FarmerId}", s3Key, farmerId);
            throw;
        }
    }

    public async Task<string> GeneratePresignedUrlAsync(
        string fileName,
        string farmerId,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var s3Key = GenerateS3Key(fileName, farmerId, "application/octet-stream");

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _config.BucketName,
                Key = s3Key,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.Add(expiration),
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
            };

            var url = await Task.Run(() => _s3Client.GetPreSignedURL(request), cancellationToken);
            
            _logger.LogInformation(
                "Generated pre-signed URL for file {FileName} for farmer {FarmerId}", 
                fileName, farmerId);

            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating pre-signed URL for file {FileName}", fileName);
            throw;
        }
    }

    public async Task DeleteAsync(
        string s3Key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _config.BucketName,
                Key = s3Key
            };

            await _s3Client.DeleteObjectAsync(request, cancellationToken);
            
            _logger.LogInformation("Deleted file {S3Key} from S3", s3Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {S3Key}", s3Key);
            throw;
        }
    }

    public async Task<string> InitiateMultipartUploadAsync(
        string fileName,
        string farmerId,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var s3Key = GenerateS3Key(fileName, farmerId, contentType);

            var request = new InitiateMultipartUploadRequest
            {
                BucketName = _config.BucketName,
                Key = s3Key,
                ContentType = contentType,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
                Metadata =
                {
                    ["farmer-id"] = farmerId,
                    ["upload-date"] = DateTimeOffset.UtcNow.ToString("o")
                }
            };

            var response = await _s3Client.InitiateMultipartUploadAsync(request, cancellationToken);
            
            _logger.LogInformation(
                "Initiated multipart upload for file {FileName} with upload ID {UploadId}", 
                fileName, response.UploadId);

            return response.UploadId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating multipart upload for file {FileName}", fileName);
            throw;
        }
    }

    private string GenerateS3Key(string fileName, string farmerId, string contentType)
    {
        var prefix = DeterminePrefix(contentType);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy/MM/dd");
        var uniqueId = Guid.NewGuid().ToString("N");
        var sanitizedFileName = SanitizeFileName(fileName);
        
        return $"{prefix}/{farmerId}/{timestamp}/{uniqueId}_{sanitizedFileName}";
    }

    private string DeterminePrefix(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            var ct when ct.StartsWith("audio/") => _config.AudioPrefix,
            var ct when ct.StartsWith("image/") => _config.ImagePrefix,
            var ct when ct.Contains("pdf") || ct.Contains("document") => _config.DocumentPrefix,
            _ => "misc"
        };
    }

    private string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
    }

    public async Task<List<string>> ListObjectsAsync(
        string prefix,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _config.BucketName,
                Prefix = prefix
            };

            var response = await _s3Client.ListObjectsV2Async(request, cancellationToken);
            
            // Defensive null checks - handle all edge cases
            if (response == null)
            {
                _logger.LogWarning("S3 ListObjectsV2 returned null response for prefix {Prefix}", prefix);
                return new List<string>();
            }
            
            if (response.S3Objects == null)
            {
                _logger.LogInformation("No S3Objects in response for prefix {Prefix} (bucket may be empty or prefix doesn't exist)", prefix);
                return new List<string>();
            }
            
            if (response.S3Objects.Count == 0)
            {
                _logger.LogInformation("Empty S3Objects list for prefix {Prefix}", prefix);
                return new List<string>();
            }
            
            // Additional safety: filter out null objects and empty keys
            var keys = response.S3Objects
                .Where(o => o != null && !string.IsNullOrEmpty(o.Key))
                .Select(o => o.Key)
                .ToList();
            
            _logger.LogInformation(
                "Listed {Count} objects with prefix {Prefix}",
                keys.Count, prefix);

            return keys;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing objects with prefix {Prefix}", prefix);
            throw;
        }
    }
}
