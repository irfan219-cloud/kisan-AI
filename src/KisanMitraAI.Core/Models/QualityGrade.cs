namespace KisanMitraAI.Core.Models;

/// <summary>
/// Enumeration of quality grades with associated multipliers
/// </summary>
public enum QualityGrade
{
    A,      // Premium quality - multiplier 1.2
    B,      // Good quality - multiplier 1.0
    C,      // Acceptable quality - multiplier 0.8
    Reject  // Below standard - multiplier 0.0
}

/// <summary>
/// Extension methods for QualityGrade
/// </summary>
public static class QualityGradeExtensions
{
    /// <summary>
    /// Gets the price multiplier for a quality grade
    /// </summary>
    public static decimal GetMultiplier(this QualityGrade grade)
    {
        return grade switch
        {
            QualityGrade.A => 1.2m,
            QualityGrade.B => 1.0m,
            QualityGrade.C => 0.8m,
            QualityGrade.Reject => 0.0m,
            _ => throw new ArgumentOutOfRangeException(nameof(grade), grade, "Invalid quality grade")
        };
    }
}
