namespace ShopAssistant.Contracts.Models.Analytics;

/// <summary>
/// Represents filter and paging criteria for querying intent log entries in analytics/admin tools.
/// </summary>
public class IntentLogQuery
{
    /// <summary>
    /// Optional. The unique identifier of the user whose logs are queried.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Optional. Only return logs for this intent (e.g. "OrderStatus").
    /// </summary>
    public string? DetectedIntent { get; set; }

    /// <summary>
    /// Optional. Only return logs for this language code (e.g. "en", "no").
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Optional. Minimum log entry score (e.g. intent confidence).
    /// </summary>
    public float? MinScore { get; set; }

    /// <summary>
    /// Optional. Maximum log entry score.
    /// </summary>
    public float? MaxScore { get; set; }

    /// <summary>
    /// Optional. Only return logs after this UTC timestamp.
    /// </summary>
    public DateTime? From { get; set; }

    /// <summary>
    /// Optional. Only return logs before this UTC timestamp.
    /// </summary>
    public DateTime? To { get; set; }

    /// <summary>
    /// Optional. Maximum number of records to return (paging).
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Optional. Offset in the result set (paging).
    /// </summary>
    public int? Offset { get; set; }
}