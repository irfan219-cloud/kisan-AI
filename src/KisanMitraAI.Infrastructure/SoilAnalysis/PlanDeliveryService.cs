using Amazon.Polly;
using Amazon.Polly.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using KisanMitraAI.Core.Models;
using KisanMitraAI.Core.SoilAnalysis;
using Microsoft.Extensions.Logging;
using System.Text;

namespace KisanMitraAI.Infrastructure.SoilAnalysis;

public class PlanDeliveryService : IPlanDeliveryService
{
    private readonly IAmazonPolly _pollyClient;
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly ILogger<PlanDeliveryService> _logger;

    // Language to voice ID mapping
    private static readonly Dictionary<string, string> VoiceMapping = new()
    {
        ["hi"] = "Aditi",      // Hindi
        ["en"] = "Kajal",      // English (Indian)
        ["ta"] = "Aditi",      // Tamil (fallback to Hindi voice)
        ["te"] = "Aditi",      // Telugu (fallback)
        ["bn"] = "Aditi",      // Bengali (fallback)
        ["mr"] = "Aditi",      // Marathi (fallback)
    };

    public PlanDeliveryService(
        IAmazonPolly pollyClient,
        IAmazonSimpleNotificationService snsClient,
        ILogger<PlanDeliveryService> logger)
    {
        _pollyClient = pollyClient ?? throw new ArgumentNullException(nameof(pollyClient));
        _snsClient = snsClient ?? throw new ArgumentNullException(nameof(snsClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> FormatPlanAsTextAsync(
        RegenerativePlan plan,
        string language,
        CancellationToken cancellationToken)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));

        var textBuilder = new StringBuilder();
        
        textBuilder.AppendLine("=== REGENERATIVE FARMING PLAN ===");
        textBuilder.AppendLine();
        textBuilder.AppendLine($"Plan ID: {plan.PlanId}");
        textBuilder.AppendLine($"Farmer ID: {plan.FarmerId}");
        textBuilder.AppendLine($"Created: {plan.CreatedAt:yyyy-MM-dd}");
        textBuilder.AppendLine();
        textBuilder.AppendLine("=== CARBON SEQUESTRATION ESTIMATE ===");
        textBuilder.AppendLine($"Total Annual: {plan.CarbonEstimate.TotalCarbonTonnesPerYear:F2} tonnes CO2e");
        textBuilder.AppendLine($"Monthly Average: {plan.CarbonEstimate.MonthlyAverageTonnes:F2} tonnes CO2e");
        textBuilder.AppendLine();
        textBuilder.AppendLine("=== MONTHLY ACTION PLAN ===");
        textBuilder.AppendLine();

        foreach (var monthlyAction in plan.MonthlyActions.OrderBy(m => m.Month))
        {
            textBuilder.AppendLine($"--- {monthlyAction.MonthName} (Month {monthlyAction.Month}) ---");
            textBuilder.AppendLine();
            textBuilder.AppendLine("Practices:");
            foreach (var practice in monthlyAction.Practices)
            {
                textBuilder.AppendLine($"  • {practice}");
            }
            textBuilder.AppendLine();
            textBuilder.AppendLine($"Rationale: {monthlyAction.Rationale}");
            textBuilder.AppendLine();
            textBuilder.AppendLine("Expected Outcomes:");
            foreach (var outcome in monthlyAction.ExpectedOutcomes)
            {
                textBuilder.AppendLine($"  • {outcome}");
            }
            textBuilder.AppendLine();

            // Add carbon estimate for this month
            var monthCarbon = plan.CarbonEstimate.MonthlyBreakdown
                .FirstOrDefault(m => m.Month == monthlyAction.Month);
            if (monthCarbon != null)
            {
                textBuilder.AppendLine($"Carbon Sequestration: {monthCarbon.EstimatedTonnes:F2} tonnes CO2e");
                textBuilder.AppendLine($"Primary Practice: {monthCarbon.PrimaryPractice}");
            }
            textBuilder.AppendLine();
        }

        return await Task.FromResult(textBuilder.ToString());
    }

    public async Task<Stream> GeneratePlanPdfAsync(
        RegenerativePlan plan,
        string language,
        CancellationToken cancellationToken)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));

        // For now, return the text version as a stream
        // In production, this would use a PDF generation library like iTextSharp or QuestPDF
        var textContent = await FormatPlanAsTextAsync(plan, language, cancellationToken);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(textContent));
        
        _logger.LogInformation("PDF generated for plan. PlanId: {PlanId}", plan.PlanId);
        
        return stream;
    }

    public async Task<Stream> SynthesizeVoiceSummaryAsync(
        RegenerativePlan plan,
        string language,
        string? voiceId,
        CancellationToken cancellationToken)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));

        // Use provided voice ID or get from mapping
        var selectedVoiceId = voiceId ?? GetVoiceIdForLanguage(language);

        // Create a summary of the plan
        var summary = CreatePlanSummary(plan);

        try
        {
            var request = new SynthesizeSpeechRequest
            {
                Text = summary,
                VoiceId = selectedVoiceId,
                OutputFormat = OutputFormat.Mp3,
                Engine = Engine.Neural,
                LanguageCode = GetLanguageCode(language)
            };

            var response = await _pollyClient.SynthesizeSpeechAsync(request, cancellationToken);

            _logger.LogInformation(
                "Voice summary synthesized. PlanId: {PlanId}, VoiceId: {VoiceId}, Language: {Language}",
                plan.PlanId, selectedVoiceId, language);

            return response.AudioStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to synthesize voice summary. PlanId: {PlanId}", plan.PlanId);
            throw new InvalidOperationException("Failed to synthesize voice summary", ex);
        }
    }

    public async Task SendNotificationAsync(
        string farmerId,
        string planId,
        NotificationType notificationType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(farmerId))
            throw new ArgumentException("Farmer ID is required", nameof(farmerId));
        if (string.IsNullOrWhiteSpace(planId))
            throw new ArgumentException("Plan ID is required", nameof(planId));

        try
        {
            var message = $"Your regenerative farming plan (ID: {planId}) is ready. " +
                         $"You can view it in the Kisan Mitra AI app.";

            // In production, this would look up the farmer's phone number and preferences
            // For now, we'll publish to an SNS topic
            var request = new PublishRequest
            {
                TopicArn = $"arn:aws:sns:us-east-1:123456789012:kisan-mitra-notifications",
                Message = message,
                Subject = "Regenerative Farming Plan Ready",
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["farmerId"] = new MessageAttributeValue { DataType = "String", StringValue = farmerId },
                    ["planId"] = new MessageAttributeValue { DataType = "String", StringValue = planId },
                    ["notificationType"] = new MessageAttributeValue { DataType = "String", StringValue = notificationType.ToString() }
                }
            };

            await _snsClient.PublishAsync(request, cancellationToken);

            _logger.LogInformation(
                "Notification sent. FarmerId: {FarmerId}, PlanId: {PlanId}, Type: {Type}",
                farmerId, planId, notificationType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send notification. FarmerId: {FarmerId}, PlanId: {PlanId}",
                farmerId, planId);
            throw new InvalidOperationException("Failed to send notification", ex);
        }
    }

    private string CreatePlanSummary(RegenerativePlan plan)
    {
        var summaryBuilder = new StringBuilder();
        
        summaryBuilder.AppendLine("Your 12-month regenerative farming plan is ready.");
        summaryBuilder.AppendLine();
        summaryBuilder.AppendLine($"This plan will help you sequester approximately " +
                                 $"{plan.CarbonEstimate.TotalCarbonTonnesPerYear:F1} tonnes of carbon per year.");
        summaryBuilder.AppendLine();
        summaryBuilder.AppendLine("Key practices include:");

        // Get top 3 most common practices
        var topPractices = plan.MonthlyActions
            .SelectMany(m => m.Practices)
            .GroupBy(p => p)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => g.Key);

        foreach (var practice in topPractices)
        {
            summaryBuilder.AppendLine($"- {practice}");
        }

        summaryBuilder.AppendLine();
        summaryBuilder.AppendLine("The plan provides detailed monthly actions for the entire year.");
        summaryBuilder.AppendLine("Please review the full plan in the app for complete details.");

        return summaryBuilder.ToString();
    }

    private string GetVoiceIdForLanguage(string language)
    {
        if (VoiceMapping.TryGetValue(language.ToLowerInvariant(), out var voiceId))
        {
            return voiceId;
        }

        return "Aditi"; // Default to Hindi voice
    }

    private string GetLanguageCode(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "hi" => "hi-IN",
            "en" => "en-IN",
            "ta" => "ta-IN",
            "te" => "te-IN",
            "bn" => "bn-IN",
            "mr" => "mr-IN",
            _ => "hi-IN"
        };
    }
}
