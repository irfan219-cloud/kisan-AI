using KisanMitraAI.Core.Authorization;
using KisanMitraAI.Core.Models;
using KisanMitraAI.Core.QualityGrading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KisanMitraAI.API.Controllers;

/// <summary>
/// Quality grading controller for produce grading (Quality Grader module)
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
// [Authorize]  // TEMPORARILY DISABLED FOR UI TESTING
// [RequiresFarmer]  // TEMPORARILY DISABLED FOR UI TESTING
public class QualityGradingController : ControllerBase
{
    private readonly IImageUploadHandler _imageUploadHandler;
    private readonly IImageAnalyzer _imageAnalyzer;
    private readonly IQualityClassifier _qualityClassifier;
    private readonly IPriceCalculator _priceCalculator;
    private readonly IGradingRecordStore _gradingRecordStore;
    private readonly ILogger<QualityGradingController> _logger;

    public QualityGradingController(
        IImageUploadHandler imageUploadHandler,
        IImageAnalyzer imageAnalyzer,
        IQualityClassifier qualityClassifier,
        IPriceCalculator priceCalculator,
        IGradingRecordStore gradingRecordStore,
        ILogger<QualityGradingController> logger)
    {
        _imageUploadHandler = imageUploadHandler;
        _imageAnalyzer = imageAnalyzer;
        _qualityClassifier = qualityClassifier;
        _priceCalculator = priceCalculator;
        _gradingRecordStore = gradingRecordStore;
        _logger = logger;
    }

    /// <summary>
    /// Grade a single produce image
    /// </summary>
    /// <param name="image">Image file of the produce (JPEG, PNG)</param>
    /// <param name="produceType">Type of produce (e.g., tomato, wheat, rice)</param>
    /// <param name="location">Location for price calculation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Grading result with grade, certified price, and analysis details</returns>
    [HttpPost("grade")]
    [EnableRateLimiting("farmer-rate-limit")]
    [ProducesResponseType(typeof(GradingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB max
    public async Task<IActionResult> GradeProduct(
        [FromForm] IFormFile image,
        [FromForm] string produceType,
        [FromForm] string location,
        CancellationToken cancellationToken)
    {
        var farmerId = User.GetFarmerId() ?? "test-farmer-123";  // Use test farmer ID when not authenticated
        
        _logger.LogInformation(
            "Grading request received from farmer {FarmerId} for produce type {ProduceType}",
            farmerId,
            produceType);

        // Validate image file
        if (image == null || image.Length == 0)
        {
            _logger.LogWarning("Empty image file received from farmer {FarmerId}", farmerId);
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "IMAGE_FILE_REQUIRED",
                Message = "Image file is required",
                UserFriendlyMessage = "कृपया एक छवि फ़ाइल अपलोड करें (Please upload an image file)",
                SuggestedActions = new[] { "Upload a valid image file in JPEG or PNG format" }
            });
        }

        // Validate image format
        var allowedFormats = new[] { ".jpg", ".jpeg", ".png" };
        var fileExtension = Path.GetExtension(image.FileName).ToLowerInvariant();
        
        if (!allowedFormats.Contains(fileExtension))
        {
            _logger.LogWarning(
                "Invalid image format {Format} received from farmer {FarmerId}",
                fileExtension,
                farmerId);
            
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "IMAGE_FORMAT_INVALID",
                Message = $"Invalid image format: {fileExtension}",
                UserFriendlyMessage = "कृपया JPEG या PNG प्रारूप में छवि अपलोड करें (Please upload image in JPEG or PNG format)",
                SuggestedActions = new[] { "Convert your image to JPEG or PNG format and try again" }
            });
        }

        // Validate file size (max 10 MB)
        if (image.Length > 10 * 1024 * 1024)
        {
            _logger.LogWarning(
                "Image file too large ({Size} bytes) from farmer {FarmerId}",
                image.Length,
                farmerId);
            
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "IMAGE_FILE_TOO_LARGE",
                Message = $"Image file size ({image.Length} bytes) exceeds maximum allowed size (10 MB)",
                UserFriendlyMessage = "छवि फ़ाइल बहुत बड़ी है। कृपया 10 MB से छोटी फ़ाइल अपलोड करें (Image file is too large. Please upload a file smaller than 10 MB)",
                SuggestedActions = new[] { "Reduce image file size to under 10 MB", "Take a photo with lower resolution" }
            });
        }

        // Validate produce type
        if (string.IsNullOrWhiteSpace(produceType))
        {
            _logger.LogWarning("Missing produce type parameter from farmer {FarmerId}", farmerId);
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "PRODUCE_TYPE_REQUIRED",
                Message = "Produce type parameter is required",
                UserFriendlyMessage = "कृपया उत्पाद का प्रकार चुनें (Please select the produce type)",
                SuggestedActions = new[] { "Specify the type of produce (e.g., tomato, wheat, rice)" }
            });
        }

        // Validate location
        if (string.IsNullOrWhiteSpace(location))
        {
            _logger.LogWarning("Missing location parameter from farmer {FarmerId}", farmerId);
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "LOCATION_REQUIRED",
                Message = "Location parameter is required",
                UserFriendlyMessage = "कृपया स्थान चुनें (Please select the location)",
                SuggestedActions = new[] { "Specify your location for price calculation" }
            });
        }

        try
        {
            // Upload image
            using var imageStream = image.OpenReadStream();
            var uploadResult = await _imageUploadHandler.UploadImageAsync(
                imageStream,
                farmerId,
                produceType,
                cancellationToken);

            if (!uploadResult.IsValid)
            {
                _logger.LogWarning(
                    "Image validation failed for farmer {FarmerId}: {Message}",
                    farmerId,
                    uploadResult.ValidationMessage);
                
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "IMAGE_QUALITY_POOR",
                    Message = uploadResult.ValidationMessage ?? "Image quality is too poor for analysis",
                    UserFriendlyMessage = "छवि की गुणवत्ता विश्लेषण के लिए बहुत खराब है। कृपया बेहतर रोशनी में एक स्पष्ट फोटो लें (Image quality is too poor for analysis. Please take a clear photo in better lighting)",
                    SuggestedActions = new[] { "Take a photo in better lighting", "Ensure the image is in focus", "Clean the camera lens" }
                });
            }

            // Analyze image
            var analysis = await _imageAnalyzer.AnalyzeImageAsync(
                uploadResult.ImageS3Key,
                cancellationToken);

            // Classify quality
            var grade = await _qualityClassifier.ClassifyQualityAsync(
                analysis,
                produceType,
                cancellationToken);

            // Calculate certified price
            var certifiedPrice = await _priceCalculator.CalculateCertifiedPriceAsync(
                grade,
                produceType,
                location,
                cancellationToken);

            // Store grading record
            var record = new GradingRecord(
                recordId: Guid.NewGuid().ToString(),
                farmerId: farmerId,
                produceType: produceType,
                grade: grade,
                certifiedPrice: certifiedPrice,
                imageS3Key: uploadResult.ImageS3Key,
                timestamp: DateTimeOffset.UtcNow,
                analysis: analysis);

            var recordId = await _gradingRecordStore.StoreGradingRecordAsync(
                record,
                cancellationToken);

            _logger.LogInformation(
                "Grading completed successfully for farmer {FarmerId}. Grade: {Grade}, Price: {Price}",
                farmerId,
                grade,
                certifiedPrice);

            var result = new GradingResult
            {
                RecordId = recordId,
                Grade = grade,
                CertifiedPrice = certifiedPrice,
                Analysis = new AnalysisDto
                {
                    ConfidenceScore = analysis.ConfidenceScore / 100f, // Convert to 0-1 range
                    QualityIndicators = new[]
                    {
                        new QualityIndicator { Name = "Size", Value = analysis.AverageSize, Status = GetStatus(analysis.AverageSize, 60, 80) },
                        new QualityIndicator { Name = "Color Uniformity", Value = analysis.ColorProfile.ColorUniformity, Status = GetStatus(analysis.ColorProfile.ColorUniformity, 50, 70) },
                        new QualityIndicator { Name = "Brightness", Value = analysis.ColorProfile.Brightness, Status = GetStatus(analysis.ColorProfile.Brightness, 40, 60) },
                        new QualityIndicator { Name = "Defects", Value = 100 - (analysis.Defects.Any() ? analysis.Defects.Average(d => d.Severity) : 0), Status = GetDefectStatus(analysis.Defects) }
                    },
                    ImageQuality = new ImageQuality
                    {
                        Resolution = "High",
                        Clarity = analysis.ConfidenceScore / 100f,
                        Lighting = analysis.ColorProfile.Brightness / 100f
                    },
                    DetectedObjects = analysis.Defects.Select(d => new DetectedObject
                    {
                        Label = d.DefectType,
                        Confidence = d.Confidence / 100f
                    }).ToArray()
                },
                Timestamp = record.Timestamp
            };

            // DEBUG: Log the exact response being sent to frontend
            Console.WriteLine($"=== GRADING RESPONSE DEBUG ===");
            Console.WriteLine($"Grade Enum Value: {result.Grade}");
            Console.WriteLine($"Grade Enum Integer: {(int)result.Grade}");
            Console.WriteLine($"Grade String: {result.Grade.ToString()}");
            Console.WriteLine($"Certified Price: {result.CertifiedPrice}");
            Console.WriteLine($"Record ID: {result.RecordId}");
            Console.WriteLine($"==============================");

            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Grading processing cancelled for farmer {FarmerId}", farmerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing grading for farmer {FarmerId}",
                farmerId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                ErrorCode = "SERVICE_UNAVAILABLE",
                Message = "An error occurred while processing your grading request",
                UserFriendlyMessage = "आपके ग्रेडिंग अनुरोध को संसाधित करते समय एक त्रुटि हुई। कृपया पुनः प्रयास करें (An error occurred while processing your grading request. Please try again)",
                SuggestedActions = new[] { "Try again in a few moments", "Check your internet connection", "Contact support if the problem persists" }
            });
        }
    }

    /// <summary>
    /// Grade a batch of produce images
    /// </summary>
    /// <param name="images">Multiple image files of the same produce batch</param>
    /// <param name="produceType">Type of produce</param>
    /// <param name="location">Location for price calculation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch grading result with aggregated grade and price</returns>
    [HttpPost("grade-batch")]
    [EnableRateLimiting("farmer-rate-limit")]
    [ProducesResponseType(typeof(BatchGradingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB max for batch
    public async Task<IActionResult> GradeBatch(
        [FromForm] List<IFormFile> images,
        [FromForm] string produceType,
        [FromForm] string location,
        CancellationToken cancellationToken)
    {
        var farmerId = User.GetFarmerId() ?? "test-farmer-123";  // Use test farmer ID when not authenticated
        
        _logger.LogInformation(
            "Batch grading request received from farmer {FarmerId} for {Count} images",
            farmerId,
            images?.Count ?? 0);

        // Validate images
        if (images == null || images.Count == 0)
        {
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "IMAGES_REQUIRED",
                Message = "At least one image is required",
                UserFriendlyMessage = "कृपया कम से कम एक छवि अपलोड करें (Please upload at least one image)",
                SuggestedActions = new[] { "Upload one or more images of your produce" }
            });
        }

        if (images.Count > 10)
        {
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "TOO_MANY_IMAGES",
                Message = "Maximum 10 images allowed per batch",
                UserFriendlyMessage = "प्रति बैच अधिकतम 10 छवियां अनुमत हैं (Maximum 10 images allowed per batch)",
                SuggestedActions = new[] { "Reduce the number of images to 10 or fewer" }
            });
        }

        try
        {
            var gradingResults = new List<GradingResult>();

            // Process each image
            foreach (var image in images)
            {
                using var imageStream = image.OpenReadStream();
                var uploadResult = await _imageUploadHandler.UploadImageAsync(
                    imageStream,
                    farmerId,
                    produceType,
                    cancellationToken);

                if (!uploadResult.IsValid)
                {
                    _logger.LogWarning(
                        "Skipping invalid image in batch for farmer {FarmerId}",
                        farmerId);
                    continue;
                }

                var analysis = await _imageAnalyzer.AnalyzeImageAsync(
                    uploadResult.ImageS3Key,
                    cancellationToken);

                var grade = await _qualityClassifier.ClassifyQualityAsync(
                    analysis,
                    produceType,
                    cancellationToken);

                var certifiedPrice = await _priceCalculator.CalculateCertifiedPriceAsync(
                    grade,
                    produceType,
                    location,
                    cancellationToken);

                var record = new GradingRecord(
                    recordId: Guid.NewGuid().ToString(),
                    farmerId: farmerId,
                    produceType: produceType,
                    grade: grade,
                    certifiedPrice: certifiedPrice,
                    imageS3Key: uploadResult.ImageS3Key,
                    timestamp: DateTimeOffset.UtcNow,
                    analysis: analysis);

                var recordId = await _gradingRecordStore.StoreGradingRecordAsync(
                    record,
                    cancellationToken);

                gradingResults.Add(new GradingResult
                {
                    RecordId = recordId,
                    Grade = grade,
                    CertifiedPrice = certifiedPrice,
                    Analysis = new AnalysisDto
                    {
                        ConfidenceScore = analysis.ConfidenceScore / 100f,
                        QualityIndicators = new[]
                        {
                            new QualityIndicator { Name = "Size", Value = analysis.AverageSize, Status = GetStatus(analysis.AverageSize, 60, 80) },
                            new QualityIndicator { Name = "Color Uniformity", Value = analysis.ColorProfile.ColorUniformity, Status = GetStatus(analysis.ColorProfile.ColorUniformity, 50, 70) },
                            new QualityIndicator { Name = "Brightness", Value = analysis.ColorProfile.Brightness, Status = GetStatus(analysis.ColorProfile.Brightness, 40, 60) },
                            new QualityIndicator { Name = "Defects", Value = 100 - (analysis.Defects.Any() ? analysis.Defects.Average(d => d.Severity) : 0), Status = GetDefectStatus(analysis.Defects) }
                        },
                        ImageQuality = new ImageQuality
                        {
                            Resolution = "High",
                            Clarity = analysis.ConfidenceScore / 100f,
                            Lighting = analysis.ColorProfile.Brightness / 100f
                        },
                        DetectedObjects = analysis.Defects.Select(d => new DetectedObject
                        {
                            Label = d.DefectType,
                            Confidence = d.Confidence / 100f
                        }).ToArray()
                    },
                    Timestamp = record.Timestamp
                });
            }

            if (gradingResults.Count == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "NO_VALID_IMAGES",
                    Message = "No valid images could be processed",
                    UserFriendlyMessage = "कोई भी मान्य छवि संसाधित नहीं की जा सकी (No valid images could be processed)",
                    SuggestedActions = new[] { "Ensure images are clear and well-lit", "Try uploading different images" }
                });
            }

            // Calculate batch-level aggregated grade (weighted average by confidence)
            var totalConfidence = gradingResults.Sum(r => r.Analysis.ConfidenceScore);
            var weightedGradeSum = gradingResults.Sum(r => 
                GetGradeNumericValue(r.Grade) * r.Analysis.ConfidenceScore);
            var aggregatedGradeValue = weightedGradeSum / totalConfidence;
            var aggregatedGrade = GetGradeFromNumericValue(aggregatedGradeValue);

            // Calculate batch-level price
            var batchPrice = await _priceCalculator.CalculateCertifiedPriceAsync(
                aggregatedGrade,
                produceType,
                location,
                cancellationToken);

            _logger.LogInformation(
                "Batch grading completed for farmer {FarmerId}. Processed: {Count}, Aggregated Grade: {Grade}",
                farmerId,
                gradingResults.Count,
                aggregatedGrade);

            return Ok(new BatchGradingResult
            {
                BatchId = Guid.NewGuid().ToString(),
                AggregatedGrade = aggregatedGrade,
                BatchCertifiedPrice = batchPrice,
                IndividualResults = gradingResults,
                ProcessedCount = gradingResults.Count,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Batch grading cancelled for farmer {FarmerId}", farmerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing batch grading for farmer {FarmerId}",
                farmerId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                ErrorCode = "SERVICE_UNAVAILABLE",
                Message = "An error occurred while processing your batch grading request",
                UserFriendlyMessage = "आपके बैच ग्रेडिंग अनुरोध को संसाधित करते समय एक त्रुटि हुई (An error occurred while processing your batch grading request)",
                SuggestedActions = new[] { "Try again in a few moments", "Try with fewer images", "Contact support if the problem persists" }
            });
        }
    }

    /// <summary>
    /// Get grading history for the authenticated farmer
    /// </summary>
    /// <param name="startDate">Start date for history query</param>
    /// <param name="endDate">End date for history query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of grading records</returns>
    [HttpGet("history")]
    [EnableRateLimiting("farmer-rate-limit")]
    [ProducesResponseType(typeof(IEnumerable<GradingRecord>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetGradingHistory(
        [FromQuery] DateTimeOffset? startDate,
        [FromQuery] DateTimeOffset? endDate,
        CancellationToken cancellationToken)
    {
        var farmerId = User.GetFarmerId() ?? "test-farmer-123";  // Use test farmer ID when not authenticated
        
        // Default to last 30 days if not specified
        var start = startDate ?? DateTimeOffset.UtcNow.AddDays(-30);
        var end = endDate ?? DateTimeOffset.UtcNow;

        if (start > end)
        {
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "INVALID_DATE_RANGE",
                Message = "Start date must be before end date",
                UserFriendlyMessage = "प्रारंभ तिथि समाप्ति तिथि से पहले होनी चाहिए (Start date must be before end date)",
                SuggestedActions = new[] { "Adjust the date range and try again" }
            });
        }

        _logger.LogInformation(
            "Grading history request from farmer {FarmerId} for period {Start} to {End}",
            farmerId,
            start,
            end);

        try
        {
            var history = await _gradingRecordStore.GetFarmerGradingHistoryAsync(
                farmerId,
                start,
                end,
                cancellationToken);

            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving grading history for farmer {FarmerId}",
                farmerId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                ErrorCode = "SERVICE_UNAVAILABLE",
                Message = "An error occurred while retrieving your grading history",
                UserFriendlyMessage = "आपका ग्रेडिंग इतिहास प्राप्त करते समय एक त्रुटि हुई (An error occurred while retrieving your grading history)",
                SuggestedActions = new[] { "Try again in a few moments", "Contact support if the problem persists" }
            });
        }
    }

    private static float GetGradeNumericValue(QualityGrade grade) => grade switch
    {
        QualityGrade.A => 4.0f,
        QualityGrade.B => 3.0f,
        QualityGrade.C => 2.0f,
        QualityGrade.Reject => 0.0f,
        _ => 0.0f
    };

    private static QualityGrade GetGradeFromNumericValue(float value) => value switch
    {
        >= 3.5f => QualityGrade.A,
        >= 2.5f => QualityGrade.B,
        >= 1.0f => QualityGrade.C,
        _ => QualityGrade.Reject
    };

    private static string GetStatus(float value, float fairThreshold, float goodThreshold)
    {
        if (value >= goodThreshold) return "good";
        if (value >= fairThreshold) return "fair";
        return "poor";
    }

    private static string GetDefectStatus(IEnumerable<Defect> defects)
    {
        if (!defects.Any()) return "good";
        var avgSeverity = defects.Average(d => d.Severity);
        if (avgSeverity < 20) return "good";
        if (avgSeverity < 40) return "fair";
        return "poor";
    }
}

/// <summary>
/// Grading result for a single image
/// </summary>
public record GradingResult
{
    public string RecordId { get; init; } = string.Empty;
    public QualityGrade Grade { get; init; }
    public decimal CertifiedPrice { get; init; }
    public AnalysisDto Analysis { get; init; } = null!;
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Analysis DTO matching frontend expectations
/// </summary>
public record AnalysisDto
{
    public float ConfidenceScore { get; init; }
    public QualityIndicator[] QualityIndicators { get; init; } = Array.Empty<QualityIndicator>();
    public ImageQuality ImageQuality { get; init; } = null!;
    public DetectedObject[] DetectedObjects { get; init; } = Array.Empty<DetectedObject>();
}

public record QualityIndicator
{
    public string Name { get; init; } = string.Empty;
    public float Value { get; init; }
    public string Status { get; init; } = string.Empty;
}

public record ImageQuality
{
    public string Resolution { get; init; } = string.Empty;
    public float Clarity { get; init; }
    public float Lighting { get; init; }
}

public record DetectedObject
{
    public string Label { get; init; } = string.Empty;
    public float Confidence { get; init; }
}

/// <summary>
/// Batch grading result for multiple images
/// </summary>
public record BatchGradingResult
{
    public string BatchId { get; init; } = string.Empty;
    public QualityGrade AggregatedGrade { get; init; }
    public decimal BatchCertifiedPrice { get; init; }
    public IEnumerable<GradingResult> IndividualResults { get; init; } = Array.Empty<GradingResult>();
    public int ProcessedCount { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
