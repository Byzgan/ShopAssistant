namespace ShopAssistant.Contracts.Models.Analytics;

/// <summary>
/// Flat representation of FAQ stats so that "Answered" and "Unanswered" are separate rows per topic.
/// This shape is useful for charts or tables that want to display each metric independently.
/// </summary>
public class FaqStatsMetric
{
    /// <summary>
    /// The FAQ topic or category. Null/empty means unknown / no topic.
    /// </summary>
    public string? Topic { get; set; }

    /// <summary>
    /// Either "Answered" or "Unanswered". 
    /// </summary>
    /// TODO: Turn into an enum.
    public string Metric { get; set; } = null!;

    /// <summary>
    /// The count of questions for this topic+metric combination.
    /// </summary>
    public int Count { get; set; }
}