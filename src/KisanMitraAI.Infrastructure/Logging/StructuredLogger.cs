using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using KisanMitraAI.Core.Logging;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace KisanMitraAI.Infrastructure.Logging;

/// <summary>
/// Structured logging service with AWS CloudWatch Logs integration
/// </summary>
public class StructuredLogger : IStructuredLogger
{
    private readonly ILogger<StructuredLogger> _logger;
    private readonly IAmazonCloudWatchLogs _cloudWatchClient;
    private readonly string _logGroupName;
    private readonly string _logStreamName;

    public StructuredLogger(
        ILogger<StructuredLogger> logger,
        IAmazonCloudWatchLogs cloudWatchClient,
        string logGroupName,
        string logStreamName)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cloudWatchClient = cloudWatchClient ?? throw new ArgumentNullException(nameof(cloudWatchClient));
        _logGroupName = logGroupName ?? throw new ArgumentNullException(nameof(logGroupName));
        _logStreamName = logStreamName ?? throw new ArgumentNullException(nameof(logStreamName));
    }

    public void LogInformation(string message, LogContext context)
    {
        var logEntry = CreateLogEntry("Information", message, context, null);
        _logger.LogInformation(logEntry);
        SendToCloudWatch(logEntry, context);
    }

    public void LogWarning(string message, LogContext context, Exception? exception = null)
    {
        var logEntry = CreateLogEntry("Warning", message, context, exception);
        _logger.LogWarning(exception, logEntry);
        SendToCloudWatch(logEntry, context);
    }

    public void LogError(string message, LogContext context, Exception exception)
    {
        var logEntry = CreateLogEntry("Error", message, context, exception);
        _logger.LogError(exception, logEntry);
        SendToCloudWatch(logEntry, context);
    }

    private string CreateLogEntry(string level, string message, LogContext context, Exception? exception)
    {
        var logData = new Dictionary<string, object?>
        {
            ["Level"] = level,
            ["Message"] = message,
            ["Timestamp"] = DateTimeOffset.UtcNow.ToString("o"),
            ["FarmerId"] = context.FarmerId,
            ["RequestId"] = context.RequestId,
            ["Module"] = context.Module,
            ["Operation"] = context.Operation,
            ["DurationMs"] = context.DurationMs,
            ["ErrorCode"] = context.ErrorCode
        };

        if (exception != null)
        {
            logData["Exception"] = new
            {
                Type = exception.GetType().FullName,
                Message = exception.Message,
                StackTrace = exception.StackTrace,
                InnerException = exception.InnerException?.Message
            };
        }

        if (context.AdditionalProperties != null)
        {
            foreach (var prop in context.AdditionalProperties)
            {
                logData[prop.Key] = prop.Value;
            }
        }

        return JsonSerializer.Serialize(logData);
    }

    private async void SendToCloudWatch(string logEntry, LogContext context)
    {
        try
        {
            var request = new PutLogEventsRequest
            {
                LogGroupName = _logGroupName,
                LogStreamName = _logStreamName,
                LogEvents = new List<InputLogEvent>
                {
                    new InputLogEvent
                    {
                        Message = logEntry,
                        Timestamp = DateTime.UtcNow
                    }
                }
            };

            await _cloudWatchClient.PutLogEventsAsync(request);
        }
        catch (Exception ex)
        {
            // Fallback to local logging if CloudWatch fails
            _logger.LogError(ex, "Failed to send log to CloudWatch: {LogEntry}", logEntry);
        }
    }
}
