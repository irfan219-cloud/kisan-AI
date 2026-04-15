using Amazon.CloudWatchLogs;
using KisanMitraAI.Core.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KisanMitraAI.Infrastructure.Logging;

/// <summary>
/// Extension methods for configuring logging services
/// </summary>
public static class LoggingServiceExtensions
{
    public static IServiceCollection AddStructuredLogging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure CloudWatch Logs client
        services.AddAWSService<IAmazonCloudWatchLogs>();

        // Register structured logger
        services.AddSingleton<IStructuredLogger>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<StructuredLogger>>();
            var cloudWatchClient = sp.GetRequiredService<IAmazonCloudWatchLogs>();
            var logGroupName = configuration["Logging:CloudWatch:LogGroupName"] ?? "KisanMitraAI";
            var logStreamName = configuration["Logging:CloudWatch:LogStreamName"] ?? $"app-{Environment.MachineName}";

            return new StructuredLogger(logger, cloudWatchClient, logGroupName, logStreamName);
        });

        // Configure log retention
        ConfigureLogRetention(services, configuration);

        return services;
    }

    private static void ConfigureLogRetention(IServiceCollection services, IConfiguration configuration)
    {
        // Log retention is configured at the CloudWatch Log Group level
        // This would typically be done via Infrastructure as Code (CDK/CloudFormation)
        // Default retention: 365 days (1 year) as per requirements
    }
}
