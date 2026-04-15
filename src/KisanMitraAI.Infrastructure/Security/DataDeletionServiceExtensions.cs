using Amazon.TimestreamWrite;
using KisanMitraAI.Core.Security;
using Microsoft.Extensions.DependencyInjection;

namespace KisanMitraAI.Infrastructure.Security;

/// <summary>
/// Extension methods for registering data deletion services
/// </summary>
public static class DataDeletionServiceExtensions
{
    /// <summary>
    /// Registers data deletion services with dependency injection
    /// </summary>
    public static IServiceCollection AddDataDeletionServices(this IServiceCollection services)
    {
        // Register AWS Timestream Write client
        services.AddAWSService<IAmazonTimestreamWrite>();
        
        // Register data deletion service
        services.AddScoped<IDataDeletionService, DataDeletionService>();

        return services;
    }
}
