using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace KisanMitraAI.Core.Authentication;

/// <summary>
/// AWS Cognito authentication service implementation
/// </summary>
public class CognitoAuthService : ICognitoAuthService
{
    private readonly IAmazonCognitoIdentityProvider _cognitoClient;
    private readonly CognitoConfiguration _configuration;
    private readonly ILogger<CognitoAuthService> _logger;
    private readonly JwtSecurityTokenHandler _jwtHandler;
    private readonly HttpClient _httpClient;

    public CognitoAuthService(
        IAmazonCognitoIdentityProvider cognitoClient,
        IOptions<CognitoConfiguration> configuration,
        ILogger<CognitoAuthService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _cognitoClient = cognitoClient ?? throw new ArgumentNullException(nameof(cognitoClient));
        _configuration = configuration?.Value ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jwtHandler = new JwtSecurityTokenHandler();
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<RegistrationResult> RegisterAsync(
        string phoneNumber,
        string password,
        string name,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Registering user with phone number: {PhoneNumber}", phoneNumber);

            var signUpRequest = new SignUpRequest
            {
                ClientId = _configuration.ClientId,
                Username = phoneNumber,
                Password = password,
                UserAttributes = new List<AttributeType>
                {
                    new AttributeType { Name = "phone_number", Value = phoneNumber },
                    new AttributeType { Name = "name", Value = name }
                }
            };

            var response = await _cognitoClient.SignUpAsync(signUpRequest, cancellationToken);

            _logger.LogInformation("User registered successfully. UserId: {UserId}, Confirmed: {Confirmed}",
                response.UserSub, response.UserConfirmed);

            return new RegistrationResult(
                Success: true,
                UserId: response.UserSub,
                RequiresConfirmation: !response.UserConfirmed.GetValueOrDefault());
        }
        catch (UsernameExistsException ex)
        {
            _logger.LogWarning(ex, "User already exists: {PhoneNumber}", phoneNumber);
            return new RegistrationResult(false, null, false, "User already exists");
        }
        catch (InvalidPasswordException ex)
        {
            _logger.LogWarning(ex, "Invalid password for registration");
            return new RegistrationResult(false, null, false, "Password does not meet requirements");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user: {PhoneNumber}", phoneNumber);
            return new RegistrationResult(false, null, false, $"Registration failed: {ex.Message}");
        }
    }

    public async Task<ConfirmationResult> ConfirmRegistrationAsync(
        string phoneNumber,
        string confirmationCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Confirming registration for phone number: {PhoneNumber}", phoneNumber);

            var confirmRequest = new ConfirmSignUpRequest
            {
                ClientId = _configuration.ClientId,
                Username = phoneNumber,
                ConfirmationCode = confirmationCode
            };

            await _cognitoClient.ConfirmSignUpAsync(confirmRequest, cancellationToken);

            _logger.LogInformation("Registration confirmed successfully for: {PhoneNumber}", phoneNumber);

            return new ConfirmationResult(Success: true);
        }
        catch (CodeMismatchException ex)
        {
            _logger.LogWarning(ex, "Invalid confirmation code for: {PhoneNumber}", phoneNumber);
            return new ConfirmationResult(false, "Invalid confirmation code");
        }
        catch (ExpiredCodeException ex)
        {
            _logger.LogWarning(ex, "Expired confirmation code for: {PhoneNumber}", phoneNumber);
            return new ConfirmationResult(false, "Confirmation code has expired");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming registration for: {PhoneNumber}", phoneNumber);
            return new ConfirmationResult(false, $"Confirmation failed: {ex.Message}");
        }
    }

    public async Task<LoginResult> LoginAsync(
        string phoneNumber,
        string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Authenticating user: {PhoneNumber}", phoneNumber);

            var authRequest = new InitiateAuthRequest
            {
                ClientId = _configuration.ClientId,
                AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
                AuthParameters = new Dictionary<string, string>
                {
                    { "USERNAME", phoneNumber },
                    { "PASSWORD", password }
                }
            };

            var response = await _cognitoClient.InitiateAuthAsync(authRequest, cancellationToken);

            if (response.AuthenticationResult == null)
            {
                _logger.LogWarning("Authentication failed - no result returned for: {PhoneNumber}", phoneNumber);
                return new LoginResult(false, null, null, null, 0, "Authentication failed");
            }

            _logger.LogInformation("User authenticated successfully: {PhoneNumber}", phoneNumber);

            return new LoginResult(
                Success: true,
                AccessToken: response.AuthenticationResult.AccessToken,
                RefreshToken: response.AuthenticationResult.RefreshToken,
                IdToken: response.AuthenticationResult.IdToken,
                ExpiresInSeconds: response.AuthenticationResult.ExpiresIn.GetValueOrDefault());
        }
        catch (NotAuthorizedException ex)
        {
            _logger.LogWarning(ex, "Invalid credentials for: {PhoneNumber}", phoneNumber);
            return new LoginResult(false, null, null, null, 0, "Invalid phone number or password");
        }
        catch (UserNotConfirmedException ex)
        {
            _logger.LogWarning(ex, "User not confirmed: {PhoneNumber}", phoneNumber);
            return new LoginResult(false, null, null, null, 0, "User registration not confirmed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authenticating user: {PhoneNumber}", phoneNumber);
            return new LoginResult(false, null, null, null, 0, $"Login failed: {ex.Message}");
        }
    }

    public async Task<RefreshTokenResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        int attempt = 0;
        Exception? lastException = null;

        while (attempt < _configuration.MaxRetryAttempts)
        {
            try
            {
                _logger.LogInformation("Refreshing token (attempt {Attempt}/{MaxAttempts})",
                    attempt + 1, _configuration.MaxRetryAttempts);

                var refreshRequest = new InitiateAuthRequest
                {
                    ClientId = _configuration.ClientId,
                    AuthFlow = AuthFlowType.REFRESH_TOKEN_AUTH,
                    AuthParameters = new Dictionary<string, string>
                    {
                        { "REFRESH_TOKEN", refreshToken }
                    }
                };

                var response = await _cognitoClient.InitiateAuthAsync(refreshRequest, cancellationToken);

                if (response.AuthenticationResult == null)
                {
                    _logger.LogWarning("Token refresh failed - no result returned");
                    return new RefreshTokenResult(false, null, null, 0, "Token refresh failed");
                }

                _logger.LogInformation("Token refreshed successfully");

                return new RefreshTokenResult(
                    Success: true,
                    AccessToken: response.AuthenticationResult.AccessToken,
                    IdToken: response.AuthenticationResult.IdToken,
                    ExpiresInSeconds: response.AuthenticationResult.ExpiresIn.GetValueOrDefault());
            }
            catch (NotAuthorizedException ex)
            {
                _logger.LogWarning(ex, "Invalid or expired refresh token");
                return new RefreshTokenResult(false, null, null, 0, "Invalid or expired refresh token");
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempt++;

                if (attempt < _configuration.MaxRetryAttempts)
                {
                    _logger.LogWarning(ex, "Token refresh failed, retrying in {Delay}ms",
                        _configuration.RetryDelayMilliseconds);
                    await Task.Delay(_configuration.RetryDelayMilliseconds, cancellationToken);
                }
            }
        }

        _logger.LogError(lastException, "Token refresh failed after {Attempts} attempts",
            _configuration.MaxRetryAttempts);

        return new RefreshTokenResult(false, null, null, 0,
            $"Token refresh failed after {_configuration.MaxRetryAttempts} attempts");
    }

    public async Task<TokenValidationResult> ValidateTokenAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Validating access token");

            // Parse the JWT token
            if (!_jwtHandler.CanReadToken(accessToken))
            {
                _logger.LogWarning("Invalid token format");
                return new TokenValidationResult(false, null, null, null, "Invalid token format");
            }

            var jwtToken = _jwtHandler.ReadJwtToken(accessToken);

            // Check token expiration
            if (jwtToken.ValidTo < DateTime.UtcNow)
            {
                _logger.LogWarning("Token has expired");
                return new TokenValidationResult(false, null, null, null, "Token has expired");
            }

            // Extract claims
            var claims = jwtToken.Claims.ToDictionary(c => c.Type, c => c.Value);
            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            var phoneNumber = jwtToken.Claims.FirstOrDefault(c => c.Type == "phone_number")?.Value;

            // Verify token with Cognito (optional but recommended for production)
            var getUserRequest = new GetUserRequest
            {
                AccessToken = accessToken
            };

            var userResponse = await _cognitoClient.GetUserAsync(getUserRequest, cancellationToken);

            _logger.LogDebug("Token validated successfully for user: {UserId}", userId);

            return new TokenValidationResult(
                IsValid: true,
                UserId: userId,
                PhoneNumber: phoneNumber,
                Claims: claims);
        }
        catch (NotAuthorizedException ex)
        {
            _logger.LogWarning(ex, "Token validation failed - not authorized");
            return new TokenValidationResult(false, null, null, null, "Token is invalid or expired");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token");
            return new TokenValidationResult(false, null, null, null, $"Token validation failed: {ex.Message}");
        }
    }

    public async Task<ConfirmationResult> AdminConfirmUserAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Admin confirming user: {PhoneNumber}", phoneNumber);

            var confirmRequest = new AdminConfirmSignUpRequest
            {
                UserPoolId = _configuration.UserPoolId,
                Username = phoneNumber
            };

            await _cognitoClient.AdminConfirmSignUpAsync(confirmRequest, cancellationToken);

            _logger.LogInformation("User confirmed successfully by admin: {PhoneNumber}", phoneNumber);

            return new ConfirmationResult(Success: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error admin confirming user: {PhoneNumber}", phoneNumber);
            return new ConfirmationResult(false, $"Admin confirmation failed: {ex.Message}");
        }
    }

    public async Task<bool> VerifyRecaptchaAsync(
        string recaptchaToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var secretKey = _configuration.RecaptchaSecretKey;
            if (string.IsNullOrEmpty(secretKey))
            {
                _logger.LogWarning("reCAPTCHA secret key not configured");
                return false;
            }

            var requestContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("secret", secretKey),
                new KeyValuePair<string, string>("response", recaptchaToken)
            });

            var response = await _httpClient.PostAsync(
                "https://www.google.com/recaptcha/api/siteverify",
                requestContent,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("reCAPTCHA verification request failed with status: {StatusCode}", response.StatusCode);
                return false;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            var recaptchaResponse = JsonSerializer.Deserialize<RecaptchaResponse>(jsonResponse);

            if (recaptchaResponse == null)
            {
                _logger.LogWarning("Failed to parse reCAPTCHA response");
                return false;
            }

            if (!recaptchaResponse.Success)
            {
                _logger.LogWarning("reCAPTCHA verification failed. Errors: {Errors}",
                    string.Join(", ", recaptchaResponse.ErrorCodes ?? Array.Empty<string>()));
                return false;
            }

            // Check score (v3 only, score ranges from 0.0 to 1.0)
            // 0.0 is very likely a bot, 1.0 is very likely a human
            if (recaptchaResponse.Score < 0.5)
            {
                _logger.LogWarning("reCAPTCHA score too low: {Score}", recaptchaResponse.Score);
                return false;
            }

            _logger.LogInformation("reCAPTCHA verified successfully. Score: {Score}, Action: {Action}",
                recaptchaResponse.Score, recaptchaResponse.Action);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying reCAPTCHA");
            return false;
        }
    }

    private class RecaptchaResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("success")]
        public bool Success { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("score")]
        public double Score { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("action")]
        public string? Action { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("challenge_ts")]
        public string? ChallengeTimestamp { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("hostname")]
        public string? Hostname { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }
    }
}
