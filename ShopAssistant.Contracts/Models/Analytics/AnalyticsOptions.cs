namespace ShopAssistant.Contracts.Models.Analytics;

public class AnalyticsOptions
{
    /// <summary>
    /// Master switch: enable/disable DB persistence.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How many days of analytics data to keep. 0 = keep all.
    /// </summary>
    public int RetentionDays { get; set; } = 30;
}