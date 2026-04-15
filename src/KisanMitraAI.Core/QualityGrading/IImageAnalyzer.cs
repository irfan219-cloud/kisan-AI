using KisanMitraAI.Core.Models;

namespace KisanMitraAI.Core.QualityGrading;

public interface IImageAnalyzer
{
    Task<ImageAnalysisResult> AnalyzeImageAsync(
        string imageS3Key,
        CancellationToken cancellationToken = default);
}
