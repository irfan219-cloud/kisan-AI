using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using KisanMitraAI.Core.Monitoring;

namespace KisanMitraAI.Infrastructure.Monitoring;

/// <summary>
/// CloudWatch metrics publisher implementation
/// </summary>
public class CloudWatchMetricsPublisher : IMetricsPublisher
{
    private readonly IAmazonCloudWatch _cloudWatchClient;
    private readonly string _namespace;

    public CloudWatchMetricsPublisher(IAmazonCloudWatch cloudWatchClient, string namespaceName = "KisanMitraAI")
    {
        _cloudWatchClient = cloudWatchClient ?? throw new ArgumentNullException(nameof(cloudWatchClient));
        _namespace = namespaceName ?? throw new ArgumentNullException(nameof(namespaceName));
    }

    public async Task PublishMetricAsync(
        string metricName,
        double value,
        MetricUnit unit,
        Dictionary<string, string>? dimensions = null)
    {
        var metricData = new MetricDatum
        {
            MetricName = metricName,
            Value = value,
            Unit = MapUnit(unit),
            Timestamp = DateTime.UtcNow
        };

        if (dimensions != null)
        {
            foreach (var dimension in dimensions)
            {
                metricData.Dimensions.Add(new Dimension
                {
                    Name = dimension.Key,
                    Value = dimension.Value
                });
            }
        }

        await PutMetricDataAsync(metricData);
    }

    public async Task PublishApiRequestAsync(string endpoint, string method, int statusCode)
    {
        var dimensions = new Dictionary<string, string>
        {
            ["Endpoint"] = endpoint,
            ["Method"] = method,
            ["StatusCode"] = statusCode.ToString()
        };

        await PublishMetricAsync("ApiRequestCount", 1, MetricUnit.Count, dimensions);
    }

    public async Task PublishApiLatencyAsync(string endpoint, string method, long durationMs)
    {
        var dimensions = new Dictionary<string, string>
        {
            ["Endpoint"] = endpoint,
            ["Method"] = method
        };

        await PublishMetricAsync("ApiLatency", durationMs, MetricUnit.Milliseconds, dimensions);
    }

    public async Task PublishErrorAsync(string module, string operation, string errorCode)
    {
        var dimensions = new Dictionary<string, string>
        {
            ["Module"] = module,
            ["Operation"] = operation,
            ["ErrorCode"] = errorCode
        };

        await PublishMetricAsync("ErrorCount", 1, MetricUnit.Count, dimensions);
    }

    public async Task PublishAiServiceInvocationAsync(string serviceName, bool success, long durationMs)
    {
        var dimensions = new Dictionary<string, string>
        {
            ["ServiceName"] = serviceName,
            ["Success"] = success.ToString()
        };

        await PublishMetricAsync("AiServiceInvocation", 1, MetricUnit.Count, dimensions);
        await PublishMetricAsync("AiServiceDuration", durationMs, MetricUnit.Milliseconds, dimensions);
    }

    public async Task PublishStepFunctionExecutionAsync(string workflowName, string status, long durationMs)
    {
        var dimensions = new Dictionary<string, string>
        {
            ["WorkflowName"] = workflowName,
            ["Status"] = status
        };

        await PublishMetricAsync("StepFunctionExecution", 1, MetricUnit.Count, dimensions);
        await PublishMetricAsync("StepFunctionDuration", durationMs, MetricUnit.Milliseconds, dimensions);
    }

    public async Task PublishQueueDepthAsync(string queueName, int depth)
    {
        var dimensions = new Dictionary<string, string>
        {
            ["QueueName"] = queueName
        };

        await PublishMetricAsync("QueueDepth", depth, MetricUnit.Count, dimensions);
    }

    public async Task PublishStorageUsageAsync(string storageType, long bytesUsed)
    {
        var dimensions = new Dictionary<string, string>
        {
            ["StorageType"] = storageType
        };

        await PublishMetricAsync("StorageUsage", bytesUsed, MetricUnit.Bytes, dimensions);
    }

    private async Task PutMetricDataAsync(MetricDatum metricData)
    {
        try
        {
            var request = new PutMetricDataRequest
            {
                Namespace = _namespace,
                MetricData = new List<MetricDatum> { metricData }
            };

            await _cloudWatchClient.PutMetricDataAsync(request);
        }
        catch (Exception)
        {
            // Silently fail to avoid impacting application performance
            // Metrics are best-effort
        }
    }

    private StandardUnit MapUnit(MetricUnit unit)
    {
        return unit switch
        {
            MetricUnit.Count => StandardUnit.Count,
            MetricUnit.Milliseconds => StandardUnit.Milliseconds,
            MetricUnit.Seconds => StandardUnit.Seconds,
            MetricUnit.Bytes => StandardUnit.Bytes,
            MetricUnit.Kilobytes => StandardUnit.Kilobytes,
            MetricUnit.Megabytes => StandardUnit.Megabytes,
            MetricUnit.Percent => StandardUnit.Percent,
            _ => StandardUnit.None
        };
    }
}
