using KisanMitraAI.Core.Models;

namespace KisanMitraAI.Core.VoiceIntelligence.Models;

/// <summary>
/// Represents a voice query history item stored in DynamoDB
/// </summary>
public record VoiceQueryHistoryItem
{
    public string QueryId { get; init; } = string.Empty;
    public string FarmerId { get; init; } = string.Empty;
    public string Transcription { get; init; } = string.Empty;
    public string ResponseText { get; init; } = string.Empty;
    public string Dialect { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public string AudioS3Key { get; init; } = string.Empty;
    public string ResponseAudioS3Key { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public bool IsFavorite { get; init; }
    public IEnumerable<MandiPrice> Prices { get; init; } = Array.Empty<MandiPrice>();
}
