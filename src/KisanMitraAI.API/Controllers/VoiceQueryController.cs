using KisanMitraAI.Core.Authorization;
using KisanMitraAI.Core.VoiceIntelligence;
using KisanMitraAI.Core.VoiceIntelligence.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KisanMitraAI.API.Controllers;

/// <summary>
/// Voice query controller for market intelligence (Krishi-Vani module)
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
//[Authorize]  // Temporarily disabled for testing
//[RequiresFarmer]
public class VoiceQueryController : ControllerBase
{
    private readonly IVoiceQueryHandler _voiceQueryHandler;
    private readonly IVoiceQueryHistoryRepository _historyRepository;
    private readonly ILogger<VoiceQueryController> _logger;

    public VoiceQueryController(
        IVoiceQueryHandler voiceQueryHandler,
        IVoiceQueryHistoryRepository historyRepository,
        ILogger<VoiceQueryController> logger)
    {
        _voiceQueryHandler = voiceQueryHandler;
        _historyRepository = historyRepository;
        _logger = logger;
    }

    /// <summary>
    /// Process a voice query for market intelligence
    /// </summary>
    /// <param name="audioFile">Audio file containing the voice query (MP3, WAV, OGG)</param>
    /// <param name="dialect">Regional dialect (e.g., Bundelkhandi, Bhojpuri, Marwari)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Voice query response with audio URL and price data</returns>
    [HttpPost("query")]
    [EnableRateLimiting("farmer-rate-limit")]
    [ProducesResponseType(typeof(VoiceQueryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB max
    public async Task<IActionResult> ProcessVoiceQuery(
        [FromForm] IFormFile audioFile,
        [FromForm] string dialect,
        CancellationToken cancellationToken)
    {
        // Temporary: Use test user ID when authorization is disabled
        var farmerId = User.GetFarmerId() ?? "74b814c8-e071-7010-1a65-ad38404fdce0"; // Test user Rajat
        
        _logger.LogInformation(
            "Voice query request received from farmer {FarmerId} with dialect {Dialect}",
            farmerId,
            dialect);

        // Validate audio file
        if (audioFile == null || audioFile.Length == 0)
        {
            _logger.LogWarning("Empty audio file received from farmer {FarmerId}", farmerId);
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "AUDIO_FILE_REQUIRED",
                Message = "Audio file is required",
                UserFriendlyMessage = "कृपया एक ऑडियो फ़ाइल अपलोड करें (Please upload an audio file)",
                SuggestedActions = new[] { "Upload a valid audio file in MP3, WAV, or OGG format" }
            });
        }

        // Validate audio format
        var allowedFormats = new[] { ".mp3", ".wav", ".ogg" };
        var fileExtension = Path.GetExtension(audioFile.FileName).ToLowerInvariant();
        
        if (!allowedFormats.Contains(fileExtension))
        {
            _logger.LogWarning(
                "Invalid audio format {Format} received from farmer {FarmerId}",
                fileExtension,
                farmerId);
            
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "AUDIO_FORMAT_INVALID",
                Message = $"Invalid audio format: {fileExtension}",
                UserFriendlyMessage = "कृपया MP3, WAV, या OGG प्रारूप में ऑडियो अपलोड करें (Please upload audio in MP3, WAV, or OGG format)",
                SuggestedActions = new[] { "Convert your audio to MP3, WAV, or OGG format and try again" }
            });
        }

        // Validate file size (max 10 MB)
        if (audioFile.Length > 10 * 1024 * 1024)
        {
            _logger.LogWarning(
                "Audio file too large ({Size} bytes) from farmer {FarmerId}",
                audioFile.Length,
                farmerId);
            
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "AUDIO_FILE_TOO_LARGE",
                Message = $"Audio file size ({audioFile.Length} bytes) exceeds maximum allowed size (10 MB)",
                UserFriendlyMessage = "ऑडियो फ़ाइल बहुत बड़ी है। कृपया 10 MB से छोटी फ़ाइल अपलोड करें (Audio file is too large. Please upload a file smaller than 10 MB)",
                SuggestedActions = new[] { "Reduce audio file size to under 10 MB", "Record a shorter audio clip" }
            });
        }

        // Validate dialect
        if (string.IsNullOrWhiteSpace(dialect))
        {
            _logger.LogWarning("Missing dialect parameter from farmer {FarmerId}", farmerId);
            return BadRequest(new ErrorResponse
            {
                ErrorCode = "DIALECT_REQUIRED",
                Message = "Dialect parameter is required",
                UserFriendlyMessage = "कृपया अपनी बोली चुनें (Please select your dialect)",
                SuggestedActions = new[] { "Specify a regional dialect (e.g., Bundelkhandi, Bhojpuri, Marwari)" }
            });
        }

        try
        {
            // Process voice query
            using var audioStream = audioFile.OpenReadStream();
            var response = await _voiceQueryHandler.ProcessVoiceQueryAsync(
                audioStream,
                dialect,
                farmerId,
                cancellationToken);

            // Save to history (fire and forget - don't block response)
            _ = Task.Run(async () =>
            {
                try
                {
                    var historyItem = new VoiceQueryHistoryItem
                    {
                        QueryId = Guid.NewGuid().ToString(),
                        FarmerId = farmerId,
                        Transcription = response.Transcription,
                        ResponseText = response.ResponseText,
                        Dialect = dialect,
                        Confidence = response.Confidence,
                        AudioS3Key = ExtractS3KeyFromUrl(response.AudioResponseUrl),
                        ResponseAudioS3Key = ExtractS3KeyFromUrl(response.AudioResponseUrl),
                        Timestamp = DateTimeOffset.UtcNow,
                        IsFavorite = false,
                        Prices = response.Prices
                    };

                    await _historyRepository.SaveQueryAsync(historyItem, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save voice query to history for farmer {FarmerId}", farmerId);
                }
            }, CancellationToken.None);

            _logger.LogInformation(
                "Voice query processed successfully for farmer {FarmerId}. Prices returned: {PriceCount}",
                farmerId,
                response.Prices.Count());

            // Map to DTO for frontend compatibility
            var responseDto = new VoiceQueryResponseDto
            {
                Transcription = response.Transcription,
                Prices = response.Prices.Select(p => new MarketPriceDto
                {
                    Commodity = p.Commodity,
                    Market = p.MandiName,
                    Price = p.ModalPrice,
                    Unit = p.Unit,
                    Date = p.PriceDate.ToString("o"), // ISO 8601 format
                    Source = p.Location
                }).ToList(),
                AudioResponseUrl = response.AudioResponseUrl,
                ResponseText = response.ResponseText,
                Confidence = response.Confidence,
                Dialect = response.Dialect
            };

            return Ok(responseDto);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Voice query processing cancelled for farmer {FarmerId}", farmerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing voice query for farmer {FarmerId}",
                farmerId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                ErrorCode = "SERVICE_UNAVAILABLE",
                Message = "An error occurred while processing your voice query",
                UserFriendlyMessage = "आपकी आवाज़ क्वेरी को संसाधित करते समय एक त्रुटि हुई। कृपया पुनः प्रयास करें (An error occurred while processing your voice query. Please try again)",
                SuggestedActions = new[] { "Try again in a few moments", "Check your internet connection", "Contact support if the problem persists" }
            });
        }
    }

    /// <summary>
    /// Get voice query history for the authenticated farmer
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(IEnumerable<VoiceQueryHistoryItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var farmerId = User.GetFarmerId() ?? "74b814c8-e071-7010-1a65-ad38404fdce0";
        
        _logger.LogInformation("Retrieving voice query history for farmer {FarmerId}", farmerId);

        var history = await _historyRepository.GetHistoryAsync(farmerId, limit, cancellationToken);
        return Ok(history);
    }

    /// <summary>
    /// Get favorite voice queries for the authenticated farmer
    /// </summary>
    [HttpGet("favorites")]
    [ProducesResponseType(typeof(IEnumerable<VoiceQueryHistoryItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFavorites(CancellationToken cancellationToken = default)
    {
        var farmerId = User.GetFarmerId() ?? "74b814c8-e071-7010-1a65-ad38404fdce0";
        
        _logger.LogInformation("Retrieving favorite queries for farmer {FarmerId}", farmerId);

        var favorites = await _historyRepository.GetFavoritesAsync(farmerId, cancellationToken);
        return Ok(favorites);
    }

    /// <summary>
    /// Toggle favorite status for a voice query
    /// </summary>
    [HttpPut("history/{queryId}/favorite")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ToggleFavorite(
        string queryId,
        [FromBody] ToggleFavoriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var farmerId = User.GetFarmerId() ?? "74b814c8-e071-7010-1a65-ad38404fdce0";
        
        _logger.LogInformation(
            "Toggling favorite status for query {QueryId} to {IsFavorite}", 
            queryId, 
            request.IsFavorite);

        await _historyRepository.ToggleFavoriteAsync(farmerId, queryId, request.IsFavorite, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Delete a voice query from history
    /// </summary>
    [HttpDelete("history/{queryId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteQuery(
        string queryId,
        CancellationToken cancellationToken = default)
    {
        var farmerId = User.GetFarmerId() ?? "74b814c8-e071-7010-1a65-ad38404fdce0";
        
        _logger.LogInformation("Deleting query {QueryId} for farmer {FarmerId}", queryId, farmerId);

        await _historyRepository.DeleteQueryAsync(farmerId, queryId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Get a specific voice query by ID with fresh presigned URL
    /// </summary>
    [HttpGet("history/{queryId}")]
    [ProducesResponseType(typeof(VoiceQueryHistoryItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetQueryById(
        string queryId,
        CancellationToken cancellationToken = default)
    {
        var farmerId = User.GetFarmerId() ?? "74b814c8-e071-7010-1a65-ad38404fdce0";
        
        _logger.LogInformation("Retrieving query {QueryId} for farmer {FarmerId}", queryId, farmerId);

        var query = await _historyRepository.GetQueryByIdAsync(farmerId, queryId, cancellationToken);
        
        if (query == null)
        {
            return NotFound(new ErrorResponse
            {
                ErrorCode = "QUERY_NOT_FOUND",
                Message = $"Query {queryId} not found",
                UserFriendlyMessage = "क्वेरी नहीं मिली (Query not found)"
            });
        }

        return Ok(query);
    }

    private string ExtractS3KeyFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return string.Empty;

        try
        {
            var uri = new Uri(url);
            // Extract the path without query parameters
            var path = uri.AbsolutePath;
            // Remove leading slash
            return path.TrimStart('/');
        }
        catch
        {
            return url;
        }
    }
}

/// <summary>
/// Standard error response format
/// </summary>
public record ErrorResponse
{
    public string ErrorCode { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string UserFriendlyMessage { get; init; } = string.Empty;
    public IEnumerable<string> SuggestedActions { get; init; } = Array.Empty<string>();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? RequestId { get; init; }
}

/// <summary>
/// Request to toggle favorite status
/// </summary>
public record ToggleFavoriteRequest
{
    public bool IsFavorite { get; init; }
}

/// <summary>
/// DTO for voice query response compatible with frontend
/// </summary>
public record VoiceQueryResponseDto
{
    public string Transcription { get; init; } = string.Empty;
    public List<MarketPriceDto> Prices { get; init; } = new();
    public string AudioResponseUrl { get; init; } = string.Empty;
    public string ResponseText { get; init; } = string.Empty;
    public float Confidence { get; init; }
    public string Dialect { get; init; } = string.Empty;
}

/// <summary>
/// DTO for market price compatible with frontend
/// </summary>
public record MarketPriceDto
{
    public string Commodity { get; init; } = string.Empty;
    public string Market { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string Date { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
}
