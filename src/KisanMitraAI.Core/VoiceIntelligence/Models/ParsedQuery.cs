namespace KisanMitraAI.Core.VoiceIntelligence.Models;

/// <summary>
/// Parsed query containing extracted commodity and location
/// </summary>
public record ParsedQuery(
    string Commodity,
    string Location,
    string Intent,
    bool RequiresClarification,
    string? ClarificationPrompt,
    float Confidence);
