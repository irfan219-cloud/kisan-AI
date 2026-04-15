using Amazon.KeyManagementService;
using KisanMitraAI.Core.Security;
using Microsoft.Extensions.DependencyInjection;

namespace KisanMitraAI.Infrastructure.Security;

/// <summary>
/// Extension methods for registering encryption services
/// </summary>
public static class EncryptionServiceExtensions
{
    /// <summary>
    /// Registers encryption services with dependency injection
    /// </summary>
    public static IServiceCollection AddEncryptionServices(this IServiceCollection services)
    {
        // Register AWS KMS client
        services.AddAWSService<IAmazonKeyManagementService>();
        
        // Register encryption service
        services.AddSingleton<IEncryptionService, EncryptionService>();

        return services;
    }
}
