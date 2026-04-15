using KisanMitraAI.Core.Models;
using KisanMitraAI.Core.QualityGrading;
using Microsoft.Extensions.Logging;

namespace KisanMitraAI.Infrastructure.Vision;

public class QualityClassifier : IQualityClassifier
{
    private readonly ILogger<QualityClassifier> _logger;

    // Grading thresholds for different produce types
    private static readonly Dictionary<string, GradingCriteria> ProduceGradingCriteria = new()
    {
        ["tomato"] = new GradingCriteria
        {
            MinSizeForA = 50.0f,  // Total coverage for bulk grading
            MinSizeForB = 30.0f,
            MinSizeForC = 15.0f,
            MaxDefectSeverityForA = 10.0f,
            MaxDefectSeverityForB = 30.0f,
            MaxDefectSeverityForC = 50.0f,
            MinColorUniformityForA = 60.0f,  // Lowered for bulk (was 80)
            MinColorUniformityForB = 40.0f,  // Lowered for bulk (was 60)
            MinColorUniformityForC = 20.0f,  // Lowered for bulk (was 40)
            MinBrightnessForA = 40.0f,
            MinBrightnessForB = 30.0f,
            MinBrightnessForC = 20.0f
        },
        ["onion"] = new GradingCriteria
        {
            MinSizeForA = 50.0f,  // Total coverage for bulk grading
            MinSizeForB = 30.0f,
            MinSizeForC = 15.0f,
            MaxDefectSeverityForA = 15.0f,
            MaxDefectSeverityForB = 35.0f,
            MaxDefectSeverityForC = 55.0f,
            MinColorUniformityForA = 55.0f,  // Lowered for bulk (was 75)
            MinColorUniformityForB = 35.0f,  // Lowered for bulk (was 55)
            MinColorUniformityForC = 20.0f,  // Lowered for bulk (was 35)
            MinBrightnessForA = 35.0f,
            MinBrightnessForB = 25.0f,
            MinBrightnessForC = 15.0f
        },
        ["potato"] = new GradingCriteria
        {
            MinSizeForA = 50.0f,  // Total coverage for bulk grading
            MinSizeForB = 30.0f,
            MinSizeForC = 15.0f,
            MaxDefectSeverityForA = 12.0f,
            MaxDefectSeverityForB = 32.0f,
            MaxDefectSeverityForC = 52.0f,
            MinColorUniformityForA = 50.0f,  // Lowered for bulk (was 70)
            MinColorUniformityForB = 30.0f,  // Lowered for bulk (was 50)
            MinColorUniformityForC = 20.0f,  // Lowered for bulk (was 30)
            MinBrightnessForA = 38.0f,
            MinBrightnessForB = 28.0f,
            MinBrightnessForC = 18.0f
        }
    };

    // Default criteria for unknown produce types
    private static readonly GradingCriteria DefaultCriteria = new()
    {
        MinSizeForA = 55.0f,
        MinSizeForB = 37.0f,
        MinSizeForC = 20.0f,
        MaxDefectSeverityForA = 12.0f,
        MaxDefectSeverityForB = 32.0f,
        MaxDefectSeverityForC = 52.0f,
        MinColorUniformityForA = 75.0f,
        MinColorUniformityForB = 55.0f,
        MinColorUniformityForC = 35.0f,
        MinBrightnessForA = 37.0f,
        MinBrightnessForB = 27.0f,
        MinBrightnessForC = 17.0f
    };

    public QualityClassifier(ILogger<QualityClassifier> logger)
    {
        _logger = logger;
    }

    public Task<QualityGrade> ClassifyQualityAsync(
        ImageAnalysisResult analysis,
        string produceType,
        CancellationToken cancellationToken = default)
    {
        // Get grading criteria for the produce type
        var criteria = ProduceGradingCriteria.GetValueOrDefault(
            produceType.ToLower(),
            DefaultCriteria);

        // Calculate defect severity score
        var defectSeverity = analysis.Defects.Any()
            ? analysis.Defects.Average(d => d.Severity)
            : 0f;

        // Determine grade based on multiple factors
        var grade = DetermineGrade(
            analysis.AverageSize,
            defectSeverity,
            analysis.ColorProfile.ColorUniformity,
            analysis.ColorProfile.Brightness,
            analysis.ConfidenceScore,
            criteria);

        _logger.LogInformation(
            "Quality classification completed for {ProduceType}. " +
            "Size: {Size} (min C: {MinC}), Defects: {DefectSeverity}, " +
            "ColorUniformity: {ColorUniformity}, Brightness: {Brightness}, Grade: {Grade}",
            produceType, analysis.AverageSize, criteria.MinSizeForC, defectSeverity,
            analysis.ColorProfile.ColorUniformity, analysis.ColorProfile.Brightness, grade);

        return Task.FromResult(grade);
    }

    private QualityGrade DetermineGrade(
        float size,
        float defectSeverity,
        float colorUniformity,
        float brightness,
        float confidenceScore,
        GradingCriteria criteria)
    {
        // Check if image quality is too poor for grading
        if (confidenceScore < 60.0f || brightness < 15.0f)
        {
            _logger.LogWarning(
                "Image quality too poor for grading. Confidence: {Confidence}, Brightness: {Brightness}",
                confidenceScore, brightness);
            return QualityGrade.Reject;
        }

        // Check for Grade A
        if (size >= criteria.MinSizeForA &&
            defectSeverity <= criteria.MaxDefectSeverityForA &&
            colorUniformity >= criteria.MinColorUniformityForA &&
            brightness >= criteria.MinBrightnessForA)
        {
            return QualityGrade.A;
        }

        // Check for Grade B
        if (size >= criteria.MinSizeForB &&
            defectSeverity <= criteria.MaxDefectSeverityForB &&
            colorUniformity >= criteria.MinColorUniformityForB &&
            brightness >= criteria.MinBrightnessForB)
        {
            return QualityGrade.B;
        }

        // Check for Grade C
        if (size >= criteria.MinSizeForC &&
            defectSeverity <= criteria.MaxDefectSeverityForC &&
            colorUniformity >= criteria.MinColorUniformityForC &&
            brightness >= criteria.MinBrightnessForC)
        {
            return QualityGrade.C;
        }

        // Log why it was rejected
        _logger.LogWarning(
            "Produce rejected. Size: {Size} (need ≥{MinC}), Defects: {Defects} (need ≤{MaxC}), " +
            "ColorUniformity: {Color} (need ≥{MinColorC}), Brightness: {Bright} (need ≥{MinBrightC})",
            size, criteria.MinSizeForC, defectSeverity, criteria.MaxDefectSeverityForC,
            colorUniformity, criteria.MinColorUniformityForC, brightness, criteria.MinBrightnessForC);

        // If none of the criteria are met, reject
        return QualityGrade.Reject;
    }

    private class GradingCriteria
    {
        public float MinSizeForA { get; init; }
        public float MinSizeForB { get; init; }
        public float MinSizeForC { get; init; }
        public float MaxDefectSeverityForA { get; init; }
        public float MaxDefectSeverityForB { get; init; }
        public float MaxDefectSeverityForC { get; init; }
        public float MinColorUniformityForA { get; init; }
        public float MinColorUniformityForB { get; init; }
        public float MinColorUniformityForC { get; init; }
        public float MinBrightnessForA { get; init; }
        public float MinBrightnessForB { get; init; }
        public float MinBrightnessForC { get; init; }
    }
}
