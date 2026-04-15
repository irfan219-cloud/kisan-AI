using KisanMitraAI.Core.Offline;
using Microsoft.Extensions.DependencyInjection;

namespace KisanMitraAI.Infrastructure.Offline;

/// <summary>
/// Extension methods for registering offline services
/// </summary>
public static class OfflineServiceExtensions
{
    /// <summary>
    /// Add offline capability services to the service collection
    /// </summary>
    public static IServiceCollection AddOfflineServices(this IServiceCollection services)
    {
        services.AddSingleton<IOfflineCacheService, OfflineCacheService>();
        services.AddSingleton<IOfflineQueueService, OfflineQueueService>();
        services.AddSingleton<IConnectivityMonitor, ConnectivityMonitor>();

        return services;
    }
}
