using Amazon.S3;
using Amazon.S3.Model;
using KisanMitraAI.Core.SoilAnalysis;
using Microsoft.Extensions.Logging;

namespace KisanMitraAI.Infrastructure.SoilAnalysis;

public class DocumentUploadHandler : IDocumentUploadHandler
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<DocumentUploadHandler> _logger;
    private readonly string _bucketName;
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    private static readonly HashSet<string> AllowedContentTypes = new()
    {
        "image/jpeg",
        "image/png",
        "application/pdf"
    };

    public DocumentUploadHandler(
        IAmazonS3 s3Client,
        ILogger<DocumentUploadHandler> logger,
        string bucketName = "kisan-mitra-documents")
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _bucketName = bucketName;
    }

    public async Task<DocumentUploadResult> UploadDocumentAsync(
        Stream documentStream,
        string farmerId,
        string documentType,
        CancellationToken cancellationToken)
    {
        if (documentStream == null)
            throw new ArgumentNullException(nameof(documentStream));
        
        if (string.IsNullOrWhiteSpace(farmerId))
            throw new ArgumentException("Farmer ID is required", nameof(farmerId));
        
        if (string.IsNullOrWhiteSpace(documentType))
            throw new ArgumentException("Document type is required", nameof(documentType));

        // Validate file size
        if (documentStream.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"Document size {documentStream.Length} bytes exceeds maximum allowed size of {MaxFileSizeBytes} bytes");
        }

        // Validate document format by reading first few bytes
        var contentType = await DetectContentTypeAsync(documentStream, cancellationToken);
        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new InvalidOperationException(
                $"Document format '{contentType}' is not supported. Allowed formats: JPEG, PNG, PDF");
        }

        // Generate S3 key with farmer-specific prefix
        var timestamp = DateTimeOffset.UtcNow;
        var fileExtension = GetFileExtension(contentType);
        var s3Key = $"documents/{farmerId}/{documentType}/{timestamp:yyyyMMdd-HHmmss}-{Guid.NewGuid()}{fileExtension}";

        try
        {
            // Upload to S3
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = s3Key,
                InputStream = documentStream,
                ContentType = contentType,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
                Metadata =
                {
                    ["farmer-id"] = farmerId,
                    ["document-type"] = documentType,
                    ["upload-timestamp"] = timestamp.ToString("O")
                }
            };

            await _s3Client.PutObjectAsync(putRequest, cancellationToken);

            _logger.LogInformation(
                "Document uploaded successfully. FarmerId: {FarmerId}, DocumentType: {DocumentType}, S3Key: {S3Key}",
                farmerId, documentType, s3Key);

            return new DocumentUploadResult(
                s3Key,
                farmerId,
                documentType,
                documentStream.Length,
                timestamp);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex,
                "Failed to upload document to S3. FarmerId: {FarmerId}, DocumentType: {DocumentType}",
                farmerId, documentType);
            throw new InvalidOperationException("Failed to upload document to storage", ex);
        }
    }

    private async Task<string> DetectContentTypeAsync(Stream stream, CancellationToken cancellationToken)
    {
        // Read first few bytes to detect file type
        var buffer = new byte[8];
        var originalPosition = stream.Position;
        
        await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
        stream.Position = originalPosition; // Reset position

        // JPEG magic numbers: FF D8 FF
        if (buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF)
            return "image/jpeg";

        // PNG magic numbers: 89 50 4E 47 0D 0A 1A 0A
        if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
            return "image/png";

        // PDF magic numbers: 25 50 44 46 (%PDF)
        if (buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46)
            return "application/pdf";

        return "application/octet-stream";
    }

    private string GetFileExtension(string contentType)
    {
        return contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "application/pdf" => ".pdf",
            _ => ".bin"
        };
    }
}
