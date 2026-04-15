using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using KisanMitraAI.Core.HistoricalAnalytics;
using KisanMitraAI.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace KisanMitraAI.Infrastructure.HistoricalAnalytics;

/// <summary>
/// Implementation of insights generator using Amazon Bedrock
/// </summary>
public class InsightsGenerator : IInsightsGenerator
{
    private readonly IAmazonBedrockRuntime _bedrockClient;
    private readonly ILogger<InsightsGenerator> _logger;
    private const string ModelId = "us.amazon.nova-pro-v1:0"; // Nova Pro inference profile

    public InsightsGenerator(
        IAmazonBedrockRuntime bedrockClient,
        ILogger<InsightsGenerator> logger)
    {
        _bedrockClient = bedrockClient ?? throw new ArgumentNullException(nameof(bedrockClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<DataPattern>> DetectPatternsAsync<T>(
        TrendData<T> trendData,
        string dataType,
        CancellationToken cancellationToken = default)
    {
        var dataPoints = trendData.DataPoints.ToList();
        
        if (dataPoints.Count < 24) // Need at least 2 years of monthly data
        {
            _logger.LogWarning("Insufficient data for pattern detection. Need at least 24 data points, got {Count}", dataPoints.Count);
            return Array.Empty<DataPattern>();
        }

        var prompt = BuildPatternDetectionPrompt(trendData, dataType);
        var response = await InvokeBedrockAsync(prompt, cancellationToken);

        return ParsePatternResponse(response, dataPoints.First().Timestamp, dataPoints.Last().Timestamp);
    }

    public async Task<IEnumerable<Insight>> GenerateInsightsAsync<T>(
        TrendData<T> trendData,
        string farmerId,
        string dataType,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildInsightsPrompt(trendData, farmerId, dataType);
        var response = await InvokeBedrockAsync(prompt, cancellationToken);

        return ParseInsightsResponse(response);
    }

    public async Task<TrendAnalysis> AnalyzeTrendAsync<T>(
        TrendData<T> trendData,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildTrendAnalysisPrompt(trendData);
        var response = await InvokeBedrockAsync(prompt, cancellationToken);

        return ParseTrendAnalysisResponse(response, trendData.Direction);
    }

    public async Task<IEnumerable<ActionSuggestion>> SuggestActionsAsync<T>(
        TrendData<T> trendData,
        string farmerId,
        string dataType,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildActionSuggestionsPrompt(trendData, farmerId, dataType);
        var response = await InvokeBedrockAsync(prompt, cancellationToken);

        return ParseActionSuggestionsResponse(response);
    }

    public async Task<IEnumerable<Insight>> GenerateComparisonInsightsAsync<T>(
        PeriodComparison<T> comparison,
        string farmerId,
        string dataType,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildComparisonInsightsPrompt(comparison, farmerId, dataType);
        var response = await InvokeBedrockAsync(prompt, cancellationToken);

        return ParseInsightsResponse(response);
    }

    private string BuildPatternDetectionPrompt<T>(TrendData<T> trendData, string dataType)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Analyze the following {dataType} data and identify patterns:");
        sb.AppendLine();
        sb.AppendLine("Data Points:");
        
        foreach (var point in trendData.DataPoints.Take(100))
        {
            sb.AppendLine($"- {point.Timestamp:yyyy-MM-dd}: {point.Value}");
        }

        sb.AppendLine();
        sb.AppendLine($"Current Trend Direction: {trendData.Direction}");
        sb.AppendLine($"Min Value: {trendData.MinValue}");
        sb.AppendLine($"Max Value: {trendData.MaxValue}");
        sb.AppendLine($"Average Value: {trendData.AverageValue}");
        sb.AppendLine();
        sb.AppendLine("Identify patterns such as:");
        sb.AppendLine("1. Seasonal patterns (recurring patterns at specific times of year)");
        sb.AppendLine("2. Cyclical patterns (longer-term cycles)");
        sb.AppendLine("3. Trending patterns (consistent upward or downward movement)");
        sb.AppendLine("4. Volatile patterns (high variability)");
        sb.AppendLine("5. Stable patterns (low variability)");
        sb.AppendLine();
        sb.AppendLine("For each pattern, provide:");
        sb.AppendLine("- Pattern type");
        sb.AppendLine("- Description");
        sb.AppendLine("- Confidence level (0-1)");
        sb.AppendLine("- Supporting evidence");

        return sb.ToString();
    }

    private string BuildInsightsPrompt<T>(TrendData<T> trendData, string farmerId, string dataType)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Generate actionable insights from the following {dataType} data for farmer {farmerId}:");
        sb.AppendLine();
        sb.AppendLine($"Trend Direction: {trendData.Direction}");
        sb.AppendLine($"Min Value: {trendData.MinValue}");
        sb.AppendLine($"Max Value: {trendData.MaxValue}");
        sb.AppendLine($"Average Value: {trendData.AverageValue}");
        sb.AppendLine($"Number of Data Points: {trendData.DataPoints.Count()}");
        
        if (trendData.Anomalies.Any())
        {
            sb.AppendLine();
            sb.AppendLine("Anomalies Detected:");
            foreach (var anomaly in trendData.Anomalies.Take(5))
            {
                sb.AppendLine($"- {anomaly.Point.Timestamp:yyyy-MM-dd}: {anomaly.Reason}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Generate insights that:");
        sb.AppendLine("1. Identify trends (improving, declining, stable)");
        sb.AppendLine("2. Highlight significant changes");
        sb.AppendLine("3. Provide context and explanations");
        sb.AppendLine("4. Suggest specific actions the farmer can take");
        sb.AppendLine();
        sb.AppendLine("Format each insight with:");
        sb.AppendLine("- Title (brief summary)");
        sb.AppendLine("- Description (detailed explanation)");
        sb.AppendLine("- Severity (Info, Warning, Critical, Positive)");
        sb.AppendLine("- Confidence (0-1)");
        sb.AppendLine("- Action suggestions");

        return sb.ToString();
    }

    private string BuildTrendAnalysisPrompt<T>(TrendData<T> trendData)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze the following trend data:");
        sb.AppendLine();
        sb.AppendLine($"Direction: {trendData.Direction}");
        sb.AppendLine($"Data Points: {trendData.DataPoints.Count()}");
        sb.AppendLine($"Min: {trendData.MinValue}, Max: {trendData.MaxValue}, Avg: {trendData.AverageValue}");
        sb.AppendLine();
        sb.AppendLine("Provide:");
        sb.AppendLine("1. Trend strength (Weak, Moderate, Strong, VeryStrong)");
        sb.AppendLine("2. Summary of the trend");
        sb.AppendLine("3. Contributing factors");
        sb.AppendLine("4. Predictions for future behavior");

        return sb.ToString();
    }

    private string BuildActionSuggestionsPrompt<T>(TrendData<T> trendData, string farmerId, string dataType)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Based on the {dataType} trend data, suggest specific actions for farmer {farmerId}:");
        sb.AppendLine();
        sb.AppendLine($"Trend: {trendData.Direction}");
        sb.AppendLine($"Current Average: {trendData.AverageValue}");
        sb.AppendLine();
        sb.AppendLine("For each action suggestion, provide:");
        sb.AppendLine("- Specific action to take");
        sb.AppendLine("- Rationale (why this action is recommended)");
        sb.AppendLine("- Priority (Low, Medium, High, Urgent)");
        sb.AppendLine("- Expected outcomes");

        return sb.ToString();
    }

    private string BuildComparisonInsightsPrompt<T>(PeriodComparison<T> comparison, string farmerId, string dataType)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Generate insights from comparing {dataType} data across multiple periods for farmer {farmerId}:");
        sb.AppendLine();
        
        foreach (var period in comparison.Periods)
        {
            sb.AppendLine($"Period: {period.Period.Label}");
            sb.AppendLine($"  Average: {period.AverageValue}");
            sb.AppendLine($"  Total: {period.TotalValue}");
            sb.AppendLine($"  Data Points: {period.DataPointCount}");
            sb.AppendLine();
        }

        sb.AppendLine("Existing Insights:");
        foreach (var insight in comparison.Insights)
        {
            sb.AppendLine($"- {insight.Description}");
        }

        sb.AppendLine();
        sb.AppendLine("Generate additional insights that:");
        sb.AppendLine("1. Identify patterns across periods");
        sb.AppendLine("2. Highlight improvements or declines");
        sb.AppendLine("3. Suggest reasons for changes");
        sb.AppendLine("4. Recommend actions based on the comparison");

        return sb.ToString();
    }

    private async Task<string> InvokeBedrockAsync(string prompt, CancellationToken cancellationToken)
    {
        try
        {
            var request = new InvokeModelRequest
            {
                ModelId = ModelId,
                ContentType = "application/json",
                Accept = "application/json",
                Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    inferenceConfig = new
                    {
                        maxTokens = 2000,
                        temperature = 0.7
                    }
                })))
            };

            var response = await _bedrockClient.InvokeModelAsync(request, cancellationToken);
            
            using var reader = new StreamReader(response.Body);
            var responseBody = await reader.ReadToEndAsync();
            var jsonResponse = JsonDocument.Parse(responseBody);
            
            // Amazon Nova response format: { "output": { "message": { "content": [{ "text": "..." }] } } }
            return jsonResponse.RootElement
                .GetProperty("output")
                .GetProperty("message")
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking Bedrock for insights generation");
            throw;
        }
    }

    private IEnumerable<DataPattern> ParsePatternResponse(string response, DateTimeOffset startDate, DateTimeOffset endDate)
    {
        // Simple parsing - in production, use more robust JSON parsing
        var patterns = new List<DataPattern>();

        if (response.Contains("seasonal", StringComparison.OrdinalIgnoreCase))
        {
            patterns.Add(new DataPattern(
                PatternType.Seasonal,
                "Seasonal pattern detected in the data",
                startDate,
                endDate,
                0.8f,
                new[] { "Recurring patterns at specific times of year" }));
        }

        if (response.Contains("trend", StringComparison.OrdinalIgnoreCase))
        {
            patterns.Add(new DataPattern(
                PatternType.Trending,
                "Consistent trend detected",
                startDate,
                endDate,
                0.85f,
                new[] { "Consistent directional movement over time" }));
        }

        return patterns;
    }

    private IEnumerable<Insight> ParseInsightsResponse(string response)
    {
        var insights = new List<Insight>();

        // Simple parsing - extract key insights from response
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        var currentInsight = new StringBuilder();
        foreach (var line in lines)
        {
            if (line.StartsWith("##") || line.StartsWith("**"))
            {
                if (currentInsight.Length > 0)
                {
                    insights.Add(CreateInsightFromText(currentInsight.ToString()));
                    currentInsight.Clear();
                }
            }
            currentInsight.AppendLine(line);
        }

        if (currentInsight.Length > 0)
        {
            insights.Add(CreateInsightFromText(currentInsight.ToString()));
        }

        return insights.Any() ? insights : new[]
        {
            new Insight(
                "Data Analysis Complete",
                response,
                InsightSeverity.Info,
                0.9f,
                Array.Empty<string>(),
                Array.Empty<ActionSuggestion>())
        };
    }

    private Insight CreateInsightFromText(string text)
    {
        var severity = text.Contains("warning", StringComparison.OrdinalIgnoreCase) ? InsightSeverity.Warning :
                      text.Contains("critical", StringComparison.OrdinalIgnoreCase) ? InsightSeverity.Critical :
                      text.Contains("positive", StringComparison.OrdinalIgnoreCase) ? InsightSeverity.Positive :
                      InsightSeverity.Info;

        return new Insight(
            "Insight",
            text.Trim(),
            severity,
            0.85f,
            Array.Empty<string>(),
            Array.Empty<ActionSuggestion>());
    }

    private TrendAnalysis ParseTrendAnalysisResponse(string response, TrendDirection direction)
    {
        var strength = response.Contains("strong", StringComparison.OrdinalIgnoreCase) ? TrendStrength.Strong :
                      response.Contains("moderate", StringComparison.OrdinalIgnoreCase) ? TrendStrength.Moderate :
                      TrendStrength.Weak;

        return new TrendAnalysis(
            direction,
            strength,
            response,
            Array.Empty<TrendFactor>(),
            Array.Empty<string>());
    }

    private IEnumerable<ActionSuggestion> ParseActionSuggestionsResponse(string response)
    {
        var suggestions = new List<ActionSuggestion>();

        // Extract action items from response
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("-") || line.TrimStart().StartsWith("*") || 
                line.TrimStart().StartsWith("1.") || line.TrimStart().StartsWith("2."))
            {
                var priority = line.Contains("urgent", StringComparison.OrdinalIgnoreCase) ? ActionPriority.Urgent :
                              line.Contains("high", StringComparison.OrdinalIgnoreCase) ? ActionPriority.High :
                              line.Contains("medium", StringComparison.OrdinalIgnoreCase) ? ActionPriority.Medium :
                              ActionPriority.Low;

                suggestions.Add(new ActionSuggestion(
                    line.Trim().TrimStart('-', '*', '1', '2', '3', '4', '5', '.', ' '),
                    "Based on historical data analysis",
                    priority,
                    Array.Empty<string>()));
            }
        }

        return suggestions.Any() ? suggestions : new[]
        {
            new ActionSuggestion(
                "Continue monitoring data trends",
                "Maintain current practices while tracking performance",
                ActionPriority.Low,
                new[] { "Better understanding of long-term patterns" })
        };
    }
}
