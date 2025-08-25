namespace ShopAssistant.Contracts.Models.Analytics;

using Enums;

/// <summary>
/// DTO for intent frequency statistics for dashboard analytics.
/// </summary>
public class IntentStats
{
    /// <summary>
    /// Intent.
    /// </summary>
    public Intent Intent { get; set; }

    /// <summary>
    /// Number of times this intent was detected in the selected period.
    /// </summary>
    public int Count { get; set; }
}