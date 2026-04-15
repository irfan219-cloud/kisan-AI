namespace KisanMitraAI.Infrastructure.Storage.S3;

/// <summary>
/// Configuration for S3 storage
/// </summary>
public class S3Configuration
{
    public string BucketName { get; set; } = "kisanmitraai-storage";
    public string Region { get; set; } = "us-east-1";
    public string AudioPrefix { get; set; } = "audio";
    public string ImagePrefix { get; set; } = "images";
    public string DocumentPrefix { get; set; } = "documents";
    
    // Lifecycle policies in days
    public int AudioRetentionDays { get; set; } = 30;
    public int ImageRetentionDays { get; set; } = 90;
    public int DocumentRetentionDays { get; set; } = -1; // Permanent
    
    // Multipart upload threshold (5 MB)
    public long MultipartThresholdBytes { get; set; } = 5 * 1024 * 1024;
    
    // Pre-signed URL expiration
    public int PresignedUrlExpirationMinutes { get; set; } = 15;
}
