using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using KisanMitraAI.Infrastructure.Repositories.DynamoDB;
using KisanMitraAI.Core.Models;

namespace KisanMitraAI.API.Controllers;

/// <summary>
/// Profile controller for managing user profile information
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IUserProfileRepository _profileRepository;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        IUserProfileRepository profileRepository,
        ILogger<ProfileController> logger)
    {
        _profileRepository = profileRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get user profile
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        // Try multiple possible phone number claim names from Cognito
        var phoneNumber = User.FindFirst("phone_number")?.Value 
            ?? User.FindFirst(ClaimTypes.MobilePhone)?.Value
            ?? User.FindFirst("cognito:username")?.Value
            ?? User.FindFirst("username")?.Value;

        _logger.LogInformation("Profile request for user: {UserId}", userId);

        try
        {
            // Try to get profile from DynamoDB
            var profile = await _profileRepository.GetProfileAsync(userId, cancellationToken);

            if (profile == null)
            {
                // Profile doesn't exist yet, return basic info from JWT claims
                var name = User.FindFirst("name")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value;
                
                return Ok(new UserProfileResponse
                {
                    Name = name ?? "",
                    PhoneNumber = phoneNumber ?? "",
                    City = "",
                    State = "",
                    Pincode = ""
                });
            }

            // Parse location string (format: "City, State, Pincode")
            var locationParts = profile.Location.Split(',', StringSplitOptions.TrimEntries);
            var city = locationParts.Length > 0 ? locationParts[0] : "";
            var state = locationParts.Length > 1 ? locationParts[1] : "";
            var pincode = locationParts.Length > 2 ? locationParts[2] : "";

            return Ok(new UserProfileResponse
            {
                Name = profile.Name,
                PhoneNumber = profile.PhoneNumber,
                City = city,
                State = state,
                Pincode = pincode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving profile for user: {UserId}", userId);
            return StatusCode(500, new { error = "Failed to retrieve profile" });
        }
    }

    /// <summary>
    /// Update user profile
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        // Log all claims for debugging
        _logger.LogInformation("All JWT claims: {Claims}", 
            string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
        
        // Try to extract phone number from JWT claims
        var phoneNumber = User.FindFirst("phone_number")?.Value 
            ?? User.FindFirst(ClaimTypes.MobilePhone)?.Value;

        // Validate that the extracted value looks like a phone number (not a UUID or other identifier)
        if (!string.IsNullOrEmpty(phoneNumber) && !LooksLikePhoneNumber(phoneNumber))
        {
            _logger.LogWarning("Extracted value doesn't look like a phone number: {Value}", phoneNumber);
            phoneNumber = null; // Reset so we can try other sources
        }

        // If phone number is still not found, try other sources
        if (string.IsNullOrEmpty(phoneNumber))
        {
            _logger.LogWarning("Phone number not found in standard claims");
            
            // Check if there's an existing profile with a phone number
            var existingProfile = await _profileRepository.GetProfileAsync(userId, cancellationToken);
            if (existingProfile != null && !string.IsNullOrEmpty(existingProfile.PhoneNumber))
            {
                phoneNumber = existingProfile.PhoneNumber;
                _logger.LogInformation("Using phone number from existing profile: {PhoneNumber}", phoneNumber);
            }
            // Check cognito:username or username claims - but validate they look like phone numbers
            else
            {
                var usernameValue = User.FindFirst("cognito:username")?.Value 
                    ?? User.FindFirst("username")?.Value;
                
                if (!string.IsNullOrEmpty(usernameValue) && LooksLikePhoneNumber(usernameValue))
                {
                    phoneNumber = usernameValue;
                    _logger.LogInformation("Using username claim as phone number: {PhoneNumber}", phoneNumber);
                }
                // Use userId as fallback if it looks like a phone number
                else if (LooksLikePhoneNumber(userId))
                {
                    phoneNumber = userId;
                    _logger.LogInformation("Using userId as phone number: {PhoneNumber}", phoneNumber);
                }
                else
                {
                    // Use a placeholder phone number for users without phone number in token
                    // This allows profile creation to proceed
                    phoneNumber = "+919999999999"; // Placeholder
                    _logger.LogWarning("No phone number found, using placeholder: {PhoneNumber}", phoneNumber);
                }
            }
        }
        else
        {
            _logger.LogInformation("Phone number extracted from token: {PhoneNumber}", phoneNumber);
        }

        _logger.LogInformation("Profile update request for user: {UserId}", userId);

        // Log the raw phone number before normalization
        _logger.LogInformation("Raw phone number before normalization: '{PhoneNumber}'", phoneNumber ?? "NULL");
        
        // Normalize phone number format (remove spaces, ensure proper format)
        phoneNumber = NormalizePhoneNumber(phoneNumber);
        _logger.LogInformation("Normalized phone number: '{PhoneNumber}'", phoneNumber ?? "NULL");
        
        // Test the phone number against the validation regex
        var testRegex = new System.Text.RegularExpressions.Regex(@"^(\+91)?[6-9]\d{9}$");
        var isValid = testRegex.IsMatch(phoneNumber ?? "");
        _logger.LogInformation("Phone number validation test: {IsValid}", isValid);

        // Validate pincode if provided
        if (!string.IsNullOrEmpty(request.Pincode) && !System.Text.RegularExpressions.Regex.IsMatch(request.Pincode, @"^\d{6}$"))
        {
            return BadRequest(new { error = "Pincode must be 6 digits" });
        }

        try
        {
            // Get existing profile or create new one
            var existingProfile = await _profileRepository.GetProfileAsync(userId, cancellationToken);

            // Build location string from city, state, pincode
            var locationParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.City)) locationParts.Add(request.City);
            if (!string.IsNullOrWhiteSpace(request.State)) locationParts.Add(request.State);
            if (!string.IsNullOrWhiteSpace(request.Pincode)) locationParts.Add(request.Pincode);
            var location = locationParts.Count > 0 ? string.Join(", ", locationParts) : "Unknown";

            // Create or update profile
            var profile = new FarmerProfile(
                farmerId: userId,
                name: request.Name,
                phoneNumber: phoneNumber,
                preferredLanguage: existingProfile?.PreferredLanguage ?? Language.Hindi,
                preferredDialect: existingProfile?.PreferredDialect,
                location: location,
                farms: existingProfile?.Farms ?? Enumerable.Empty<FarmProfile>(),
                registeredAt: existingProfile?.RegisteredAt ?? DateTimeOffset.UtcNow
            );

            // Save to DynamoDB
            await _profileRepository.SaveProfileAsync(profile, cancellationToken);

            _logger.LogInformation("Profile updated successfully for user: {UserId}", userId);

            // Return updated profile
            return Ok(new UserProfileResponse
            {
                Name = request.Name,
                PhoneNumber = phoneNumber,
                City = request.City ?? "",
                State = request.State ?? "",
                Pincode = request.Pincode ?? ""
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile for user: {UserId}", userId);
            return StatusCode(500, new { error = "Failed to update profile" });
        }
    }

    /// <summary>
    /// Check if a string looks like a phone number (not a UUID or other identifier)
    /// </summary>
    private static bool LooksLikePhoneNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Remove common phone number formatting characters
        var cleaned = System.Text.RegularExpressions.Regex.Replace(value, @"[\s\-\(\)]", "");

        // Check if it starts with + followed by digits, or is all digits
        // Phone numbers should be 10-15 digits
        if (cleaned.StartsWith("+"))
        {
            var digitsOnly = cleaned.Substring(1);
            return digitsOnly.Length >= 10 && digitsOnly.Length <= 15 && digitsOnly.All(char.IsDigit);
        }

        // Check if it's 10-15 digits
        return cleaned.Length >= 10 && cleaned.Length <= 15 && cleaned.All(char.IsDigit);
    }

    /// <summary>
    /// Normalize phone number to Indian format
    /// </summary>
    private static string NormalizePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return phoneNumber;
        }

        // Remove all spaces, dashes, and parentheses
        phoneNumber = System.Text.RegularExpressions.Regex.Replace(phoneNumber, @"[\s\-\(\)]", "");

        // If it starts with +91, keep it as is
        if (phoneNumber.StartsWith("+91"))
        {
            return phoneNumber;
        }

        // If it starts with 91 and is 12 digits, add +
        if (phoneNumber.StartsWith("91") && phoneNumber.Length == 12)
        {
            return "+" + phoneNumber;
        }

        // If it's 10 digits starting with 6-9, add +91
        if (phoneNumber.Length == 10 && phoneNumber[0] >= '6' && phoneNumber[0] <= '9')
        {
            return "+91" + phoneNumber;
        }

        // Return as is if we can't normalize
        return phoneNumber;
    }
}

/// <summary>
/// User profile response model
/// </summary>
public class UserProfileResponse
{
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Pincode { get; set; } = string.Empty;
}

/// <summary>
/// Update profile request model
/// </summary>
public class UpdateProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }
}
