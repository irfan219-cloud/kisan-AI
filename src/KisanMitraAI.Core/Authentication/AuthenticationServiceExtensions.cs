using Amazon.CognitoIdentityProvider;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KisanMitraAI.Core.Authentication;

/// <summary>
/// Extension methods for registering authentication services
/// </summary>
public static class AuthenticationServiceExtensions
{
    /// <summary>
    /// Adds AWS Cognito authentication services to the service collection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration containing Cognito settings</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddCognitoAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<CognitoConfiguration>(
            configuration.GetSection("Cognito"));

        // Register AWS Cognito client
        services.AddSingleton<IAmazonCognitoIdentityProvider>(sp =>
        {
            var config = configuration.GetSection("Cognito").Get<CognitoConfiguration>();
            if (config == null || string.IsNullOrEmpty(config.Region))
            {
                throw new InvalidOperationException("Cognito configuration is missing or invalid");
            }

            return new AmazonCognitoIdentityProviderClient(
                Amazon.RegionEndpoint.GetBySystemName(config.Region));
        });

        // Register authentication service
        services.AddScoped<ICognitoAuthService, CognitoAuthService>();

        return services;
    }
}
