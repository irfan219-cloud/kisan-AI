using Amazon.CloudWatch;
using Amazon.SimpleNotificationService;
using KisanMitraAI.Core.Monitoring;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KisanMitraAI.Infrastructure.Monitoring;

/// <summary>
/// Extension methods for configuring monitoring services
/// </summary>
public static class MonitoringServiceExtensions
{
    public static IServiceCollection AddMonitoring(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure CloudWatch client
        services.AddAWSService<IAmazonCloudWatch>();

        // Configure SNS client for alerts
        services.AddAWSService<IAmazonSimpleNotificationService>();

        // Register metrics publisher
        services.AddSingleton<IMetricsPublisher>(sp =>
        {
            var cloudWatchClient = sp.GetRequiredService<IAmazonCloudWatch>();
            var namespaceName = configuration["Monitoring:CloudWatch:Namespace"] ?? "KisanMitraAI";
            return new CloudWatchMetricsPublisher(cloudWatchClient, namespaceName);
        });

        // Register alert configuration
        services.Configure<AlertConfiguration>(configuration.GetSection("Monitoring:Alerts"));

        return services;
    }
}
