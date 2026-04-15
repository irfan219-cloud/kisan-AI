using KisanMitraAI.Core.Authorization;
using KisanMitraAI.Core.Models;
using KisanMitraAI.Core.PlantingAdvisory;
using KisanMitraAI.Infrastructure.Storage.S3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text;
using System.Text.Json;

namespace KisanMitraAI.API.Controllers;

/// <summary>
/// Planting advisory controller for predictive planting recommendations (Sowing Oracle module)
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
//[Authorize]
//[RequiresFarmer]
public class PlantingAdvisoryController : ControllerBase
{
    private readonly IWeatherDataCollector _weatherDataCollector;
    private readonly ISoilDataRetriever _soilDataRetriever;
    private readonly IPlantingWindowAnalyzer _plantingWindowAnalyzer;
    private readonly ISeedVarietyRecommender _seedVarietyRecommender;
    private readonly IS3StorageService _s3StorageService;
    private readonly ILogger<PlantingAdvisoryController> _logger;

    public PlantingAdvisoryController(
        IWeatherDataCollector weatherDataCollector,
        ISoilDataRetriever soilDataRetriever,
        IPlantingWindowAnalyzer plantingWindowAnalyzer,
        ISeedVarietyRecommender seedVarietyRecommender,
        IS3StorageService s3StorageService,
        ILogger<PlantingAdvisoryController> logger)
    {
        _weatherDataCollector = weatherDataCollector;
        _soilDataRetriever = soilDataRetriever;
        _plantingWindowAnalyzer = plantingWindowAnalyzer;
        _seedVarietyRecommender = seedVarietyRecommender;
        _s3StorageService = s3StorageService;
        _logger = logger;
    }

    /// <summary>
    /// Get planting recommendations for a specific crop and location
    /// </summary>
    /// <param name="request">Planting recommendation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Planting recommendation with windows, varieties, and confidence scores</returns>
    [HttpPost("recommend")]
    [EnableRateLimiting("farmer-rate-limit")]
    [ProducesResponseType(typeof(PlantingRecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPlantingRecommendation(
        [FromBody] PlantingRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        var farmerId = User.GetFarmerId();
        
        _logger.LogInformation(
            "Planting recommendation request received from farmer {FarmerId} for crop {CropType} at location {Location}",
            farmerId,
            request.CropType,
            request.Location);

        // Validate crop type
        if (string.IsNullOrWhiteSpace(request.CropType))
        {
            _logger.LogWarning("Missing crop type parameter from farmer {FarmerId}", farmerId);
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "CROP_TYPE_REQUIRED",
                Message = "Crop type parameter is required",
                UserFriendlyMessage = "कृपया फसल का प्रकार चुनें (Please select the crop type)",
                SuggestedActions = new[] { "Specify the type of crop you want to plant (e.g., wheat, rice, cotton)" }
            });
        }

        // Validate location
        if (string.IsNullOrWhiteSpace(request.Location))
        {
            _logger.LogWarning("Missing location parameter from farmer {FarmerId}", farmerId);
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "LOCATION_REQUIRED",
                Message = "Location parameter is required",
                UserFriendlyMessage = "कृपया स्थान चुनें (Please select the location)",
                SuggestedActions = new[] { "Specify your farm location (city/district)" }
            });
        }

        // Validate forecast days
        var daysAhead = request.ForecastDays ?? 90;
        if (daysAhead < 1 || daysAhead > 90)
        {
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "INVALID_FORECAST_DAYS",
                Message = "Forecast days must be between 1 and 90",
                UserFriendlyMessage = "पूर्वानुमान दिन 1 से 90 के बीच होने चाहिए (Forecast days must be between 1 and 90)",
                SuggestedActions = new[] { "Adjust the forecast days to a value between 1 and 90" }
            });
        }

        try
        {
            // Retrieve weather forecast
            _logger.LogInformation(
                "Fetching weather forecast for location {Location}, {Days} days ahead",
                request.Location,
                daysAhead);

            var weatherForecast = await _weatherDataCollector.GetForecastAsync(
                request.Location,
                daysAhead,
                cancellationToken);

            // Retrieve soil data
            _logger.LogInformation(
                "Retrieving soil data for farmer {FarmerId}",
                farmerId);

            var soilData = await _soilDataRetriever.GetLatestSoilDataAsync(
                farmerId,
                cancellationToken);

            if (soilData == null)
            {
                _logger.LogWarning(
                    "No soil data found for farmer {FarmerId}",
                    farmerId);

                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "SOIL_DATA_NOT_FOUND",
                    Message = "No soil data found for your farm",
                    UserFriendlyMessage = "आपके खेत के लिए कोई मृदा डेटा नहीं मिला। कृपया पहले अपना मृदा स्वास्थ्य कार्ड अपलोड करें (No soil data found for your farm. Please upload your Soil Health Card first)",
                    SuggestedActions = new[] { "Upload your Soil Health Card to get soil data", "Contact support if you have already uploaded your card" }
                });
            }

            // Analyze planting windows
            _logger.LogInformation(
                "Analyzing planting windows for crop {CropType}",
                request.CropType);

            var plantingWindows = await _plantingWindowAnalyzer.AnalyzePlantingWindowsAsync(
                weatherForecast,
                soilData,
                request.CropType,
                cancellationToken);

            var windowsList = plantingWindows.ToList();

            if (!windowsList.Any())
            {
                _logger.LogWarning(
                    "No suitable planting windows found for farmer {FarmerId}, crop {CropType}",
                    farmerId,
                    request.CropType);

                return Ok(new PlantingRecommendationResponse
                {
                    PlantingWindows = Array.Empty<PlantingWindow>(),
                    SeedRecommendations = Array.Empty<SeedRecommendation>(),
                    Message = "वर्तमान मौसम की स्थिति के आधार पर कोई उपयुक्त रोपण खिड़की नहीं मिली (No suitable planting windows found based on current weather conditions)",
                    HasRecommendations = false
                });
            }

            // Get seed variety recommendations for the best planting window
            var bestWindow = windowsList.OrderByDescending(w => w.ConfidenceScore).First();

            _logger.LogInformation(
                "Recommending seed varieties for best planting window ({Start} to {End})",
                bestWindow.StartDate,
                bestWindow.EndDate);

            var seedRecommendations = await _seedVarietyRecommender.RecommendVarietiesAsync(
                bestWindow,
                soilData,
                request.CropType,
                cancellationToken);

            _logger.LogInformation(
                "Planting recommendations generated successfully for farmer {FarmerId}. Windows: {WindowCount}, Varieties: {VarietyCount}",
                farmerId,
                windowsList.Count,
                seedRecommendations.Count());

            return Ok(new PlantingRecommendationResponse
            {
                PlantingWindows = windowsList,
                SeedRecommendations = seedRecommendations,
                Message = "रोपण सिफारिशें सफलतापूर्वक उत्पन्न की गईं (Planting recommendations generated successfully)",
                HasRecommendations = true,
                WeatherFetchedAt = weatherForecast.FetchedAt,
                SoilDataDate = soilData.TestDate
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Planting recommendation processing cancelled for farmer {FarmerId}", farmerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error generating planting recommendations for farmer {FarmerId}",
                farmerId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                ErrorCode = "SERVICE_UNAVAILABLE",
                Message = "An error occurred while generating planting recommendations",
                UserFriendlyMessage = "रोपण सिफारिशें उत्पन्न करते समय एक त्रुटि हुई। कृपया पुनः प्रयास करें (An error occurred while generating planting recommendations. Please try again)",
                SuggestedActions = new[] { "Try again in a few moments", "Check your internet connection", "Contact support if the problem persists" }
            });
        }
    }

    /// <summary>
    /// Get planting recommendations using soil data from a saved plan
    /// </summary>
    /// <param name="request">Planting recommendation request with plan ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Planting recommendation with windows, varieties, and confidence scores</returns>
    [HttpPost("recommend-from-plan")]
    [EnableRateLimiting("farmer-rate-limit")]
    [ProducesResponseType(typeof(PlantingRecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPlantingRecommendationFromPlan(
        [FromBody] PlantingRecommendationFromPlanRequest request,
        CancellationToken cancellationToken)
    {
        var farmerId = User.GetFarmerId() ?? "a47854d8-40a1-709a-edfa-8c09543849a1";
        
        _logger.LogInformation(
            "Planting recommendation request from saved plan {PlanId} received from farmer {FarmerId} for crop {CropType}",
            request.PlanId,
            farmerId,
            request.CropType);

        // Validate plan ID
        if (string.IsNullOrWhiteSpace(request.PlanId))
        {
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "PLAN_ID_REQUIRED",
                Message = "Plan ID is required",
                UserFriendlyMessage = "कृपया एक सहेजी गई योजना चुनें (Please select a saved plan)",
                SuggestedActions = new[] { "Select a saved soil analysis plan from the dropdown" }
            });
        }

        // Validate crop type
        if (string.IsNullOrWhiteSpace(request.CropType))
        {
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "CROP_TYPE_REQUIRED",
                Message = "Crop type parameter is required",
                UserFriendlyMessage = "कृपया फसल का प्रकार चुनें (Please select the crop type)",
                SuggestedActions = new[] { "Specify the type of crop you want to plant" }
            });
        }

        // Validate location
        if (string.IsNullOrWhiteSpace(request.Location))
        {
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "LOCATION_REQUIRED",
                Message = "Location parameter is required",
                UserFriendlyMessage = "कृपया स्थान चुनें (Please select the location)",
                SuggestedActions = new[] { "Specify your farm location" }
            });
        }

        var daysAhead = request.ForecastDays ?? 90;
        if (daysAhead < 1 || daysAhead > 90)
        {
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "INVALID_FORECAST_DAYS",
                Message = "Forecast days must be between 1 and 90",
                UserFriendlyMessage = "पूर्वानुमान दिन 1 से 90 के बीच होने चाहिए",
                SuggestedActions = new[] { "Adjust the forecast days to a value between 1 and 90" }
            });
        }

        try
        {
            // Retrieve soil data from the saved plan
            _logger.LogInformation(
                "Retrieving soil data from saved plan {PlanId} for farmer {FarmerId}",
                request.PlanId,
                farmerId);

            var soilData = await _soilDataRetriever.GetSoilDataFromPlanAsync(
                farmerId,
                request.PlanId,
                cancellationToken);

            if (soilData == null)
            {
                _logger.LogWarning(
                    "Saved plan {PlanId} not found for farmer {FarmerId}",
                    request.PlanId,
                    farmerId);

                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "PLAN_NOT_FOUND",
                    Message = "The selected plan was not found",
                    UserFriendlyMessage = "चयनित योजना नहीं मिली। कृपया दूसरी योजना चुनें (Selected plan not found. Please select another plan)",
                    SuggestedActions = new[] { "Select a different saved plan", "Create a new soil analysis" }
                });
            }

            // Retrieve weather forecast
            _logger.LogInformation(
                "Fetching weather forecast for location {Location}, {Days} days ahead",
                request.Location,
                daysAhead);

            var weatherForecast = await _weatherDataCollector.GetForecastAsync(
                request.Location,
                daysAhead,
                cancellationToken);

            // Analyze planting windows
            _logger.LogInformation(
                "Analyzing planting windows for crop {CropType} using plan {PlanId}",
                request.CropType,
                request.PlanId);

            var plantingWindows = await _plantingWindowAnalyzer.AnalyzePlantingWindowsAsync(
                weatherForecast,
                soilData,
                request.CropType,
                cancellationToken);

            var windowsList = plantingWindows.ToList();

            if (!windowsList.Any())
            {
                return Ok(new PlantingRecommendationResponse
                {
                    PlantingWindows = Array.Empty<PlantingWindow>(),
                    SeedRecommendations = Array.Empty<SeedRecommendation>(),
                    Message = "वर्तमान मौसम की स्थिति के आधार पर कोई उपयुक्त रोपण खिड़की नहीं मिली",
                    HasRecommendations = false
                });
            }

            // Get seed variety recommendations
            var bestWindow = windowsList.OrderByDescending(w => w.ConfidenceScore).First();

            var seedRecommendations = await _seedVarietyRecommender.RecommendVarietiesAsync(
                bestWindow,
                soilData,
                request.CropType,
                cancellationToken);

            _logger.LogInformation(
                "Planting recommendations generated successfully from plan {PlanId} for farmer {FarmerId}",
                request.PlanId,
                farmerId);

            return Ok(new PlantingRecommendationResponse
            {
                PlantingWindows = windowsList,
                SeedRecommendations = seedRecommendations,
                Message = "रोपण सिफारिशें सफलतापूर्वक उत्पन्न की गईं (Planting recommendations generated successfully)",
                HasRecommendations = true,
                WeatherFetchedAt = weatherForecast.FetchedAt,
                SoilDataDate = soilData.TestDate,
                UsedPlanId = request.PlanId
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Planting recommendation processing cancelled for farmer {FarmerId}", farmerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error generating planting recommendations from plan {PlanId} for farmer {FarmerId}",
                request.PlanId,
                farmerId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                ErrorCode = "SERVICE_UNAVAILABLE",
                Message = "An error occurred while generating planting recommendations",
                UserFriendlyMessage = "रोपण सिफारिशें उत्पन्न करते समय एक त्रुटि हुई। कृपया पुनः प्रयास करें",
                SuggestedActions = new[] { "Try again in a few moments", "Contact support if the problem persists" }
            });
        }
    }

    /// <summary>
    /// Save a planting recommendation to S3
    /// </summary>
    /// <param name="request">The recommendation to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Saved recommendation confirmation</returns>
    [HttpPost("recommendations/save")]
    [EnableRateLimiting("farmer-rate-limit")]
    [ProducesResponseType(typeof(SavedRecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SaveRecommendation(
        [FromBody] SaveRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        var farmerId = User.GetFarmerId() ?? "test-farmer-123";

        _logger.LogInformation(
            "Save recommendation request received from farmer {FarmerId}",
            farmerId);

        try
        {
            var recommendationJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(recommendationJson));
            var s3Key = await _s3StorageService.UploadAsync(
                stream,
                $"planting-recommendation-{request.RecommendationId}.json",
                farmerId,
                "application/json",
                cancellationToken);

            _logger.LogInformation(
                "Recommendation {RecommendationId} saved successfully for farmer {FarmerId} at {S3Key}",
                request.RecommendationId,
                farmerId,
                s3Key);

            return Ok(new SavedRecommendationResponse
            {
                RecommendationId = request.RecommendationId,
                S3Key = s3Key,
                SavedAt = DateTimeOffset.UtcNow,
                Message = "सिफारिश सफलतापूर्वक सहेजी गई (Recommendation saved successfully)"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error saving recommendation {RecommendationId} for farmer {FarmerId}",
                request.RecommendationId,
                farmerId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                ErrorCode = "RECOMMENDATION_SAVE_FAILED",
                Message = "An error occurred while saving your recommendation",
                UserFriendlyMessage = "आपकी सिफारिश सहेजते समय एक त्रुटि हुई (An error occurred while saving your recommendation)",
                SuggestedActions = new[] { "Try again in a few moments", "Contact support if the problem persists" }
            });
        }
    }

    /// <summary>
    /// Get all saved recommendations for the authenticated farmer
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of saved recommendations</returns>
    [HttpGet("recommendations")]
    [EnableRateLimiting("farmer-rate-limit")]
    [ProducesResponseType(typeof(IEnumerable<SaveRecommendationRequest>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSavedRecommendations(CancellationToken cancellationToken)
    {
        var farmerId = User.GetFarmerId() ?? "test-farmer-123";

        _logger.LogInformation(
            "Get saved recommendations request from farmer {FarmerId}",
            farmerId);

        try
        {
            // List all recommendation files for this farmer
            var prefix = $"misc/{farmerId}/";
            var s3Keys = await _s3StorageService.ListObjectsAsync(prefix, cancellationToken);

            var recommendations = new List<SaveRecommendationRequest>();

            foreach (var s3Key in s3Keys.Where(k => k.Contains("planting-recommendation-") && k.EndsWith(".json")))
            {
                try
                {
                    using var stream = await _s3StorageService.DownloadAsync(s3Key, farmerId, cancellationToken);
                    using var reader = new StreamReader(stream);
                    var json = await reader.ReadToEndAsync(cancellationToken);
                    var recommendation = JsonSerializer.Deserialize<SaveRecommendationRequest>(json);
                    
                    if (recommendation != null)
                    {
                        recommendations.Add(recommendation);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to load recommendation from {S3Key} for farmer {FarmerId}",
                        s3Key,
                        farmerId);
                }
            }

            _logger.LogInformation(
                "Retrieved {Count} saved recommendations for farmer {FarmerId}",
                recommendations.Count,
                farmerId);

            return Ok(recommendations.OrderByDescending(r => r.SavedAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving saved recommendations for farmer {FarmerId}",
                farmerId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                ErrorCode = "RECOMMENDATIONS_RETRIEVAL_FAILED",
                Message = "An error occurred while retrieving your saved recommendations",
                UserFriendlyMessage = "आपकी सहेजी गई सिफारिशें प्राप्त करते समय एक त्रुटि हुई (An error occurred while retrieving your saved recommendations)",
                SuggestedActions = new[] { "Try again in a few moments", "Contact support if the problem persists" }
            });
        }
    }

    /// <summary>
    /// Delete a saved recommendation
    /// </summary>
    /// <param name="recommendationId">Recommendation ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deletion confirmation</returns>
    [HttpDelete("recommendations/{recommendationId}")]
    [EnableRateLimiting("farmer-rate-limit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteRecommendation(
        string recommendationId,
        CancellationToken cancellationToken)
    {
        var farmerId = User.GetFarmerId() ?? "test-farmer-123";

        _logger.LogInformation(
            "Delete recommendation {RecommendationId} request from farmer {FarmerId}",
            recommendationId,
            farmerId);

        try
        {
            // List all recommendation files for this farmer to find the matching one
            var prefix = $"misc/{farmerId}/";
            var s3Keys = await _s3StorageService.ListObjectsAsync(prefix, cancellationToken);

            var recommendationKey = s3Keys.FirstOrDefault(k => k.Contains($"planting-recommendation-{recommendationId}.json"));

            if (recommendationKey == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "RECOMMENDATION_NOT_FOUND",
                    Message = $"Recommendation {recommendationId} not found",
                    UserFriendlyMessage = "सिफारिश नहीं मिली (Recommendation not found)",
                    SuggestedActions = new[] { "Check the recommendation ID and try again" }
                });
            }

            await _s3StorageService.DeleteAsync(recommendationKey, cancellationToken);

            _logger.LogInformation(
                "Recommendation {RecommendationId} deleted successfully for farmer {FarmerId}",
                recommendationId,
                farmerId);

            return Ok(new
            {
                Message = "सिफारिश सफलतापूर्वक हटाई गई (Recommendation deleted successfully)",
                RecommendationId = recommendationId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error deleting recommendation {RecommendationId} for farmer {FarmerId}",
                recommendationId,
                farmerId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                ErrorCode = "RECOMMENDATION_DELETION_FAILED",
                Message = "An error occurred while deleting the recommendation",
                UserFriendlyMessage = "सिफारिश हटाते समय एक त्रुटि हुई (An error occurred while deleting the recommendation)",
                SuggestedActions = new[] { "Try again in a few moments", "Contact support if the problem persists" }
            });
        }
    }
}

/// <summary>
/// Request for planting recommendations
/// </summary>
public record PlantingRecommendationRequest
{
    public string CropType { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public int? ForecastDays { get; init; }
}

/// <summary>
/// Response with planting recommendations
/// </summary>
public record PlantingRecommendationResponse
{
    public IEnumerable<PlantingWindow> PlantingWindows { get; init; } = Array.Empty<PlantingWindow>();
    public IEnumerable<SeedRecommendation> SeedRecommendations { get; init; } = Array.Empty<SeedRecommendation>();
    public string Message { get; init; } = string.Empty;
    public bool HasRecommendations { get; init; }
    public DateTimeOffset? WeatherFetchedAt { get; init; }
    public DateTimeOffset? SoilDataDate { get; init; }
    public string? UsedPlanId { get; init; }
}

/// <summary>
/// Request for planting recommendations using a saved soil plan
/// </summary>
public record PlantingRecommendationFromPlanRequest
{
    public string PlanId { get; init; } = string.Empty;
    public string CropType { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public int? ForecastDays { get; init; }
}

/// <summary>
/// Request to save a planting recommendation
/// </summary>
public record SaveRecommendationRequest
{
    public string RecommendationId { get; init; } = string.Empty;
    public string CropType { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public IEnumerable<PlantingWindow> PlantingWindows { get; init; } = Array.Empty<PlantingWindow>();
    public IEnumerable<SeedRecommendation> SeedRecommendations { get; init; } = Array.Empty<SeedRecommendation>();
    public DateTimeOffset? WeatherFetchedAt { get; init; }
    public DateTimeOffset? SoilDataDate { get; init; }
    public string SavedAt { get; init; } = string.Empty;
}

/// <summary>
/// Response for saved recommendation
/// </summary>
public record SavedRecommendationResponse
{
    public string RecommendationId { get; init; } = string.Empty;
    public string S3Key { get; init; } = string.Empty;
    public DateTimeOffset SavedAt { get; init; }
    public string Message { get; init; } = string.Empty;
}
