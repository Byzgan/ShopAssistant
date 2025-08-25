namespace ShopAssistant.Contracts.Models.Analytics;

using Enums;

/// <summary>
/// Represents a log entry for intent detection analytics.
/// </summary>
public class IntentLogEntry
{
    /// <summary>
    /// Primary key (auto-increment).
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Unique user identifier (nullable for unauthenticated users).
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// The user's role.
    /// </summary>
    public required int UserRole { get; set; }

    /// <summary>
    /// The external system or integration source.
    /// </summary>
    public required string ExternalSystem { get; set; }

    /// <summary>
    /// The original user message/input.
    /// </summary>
    public string InputText { get; set; } = null!;

    /// <summary>
    /// The detected intent name (string).
    /// </summary>
    public Intent DetectedIntent { get; set; }

    /// <summary>
    /// Confidence score (semantic similarity or rule match).
    /// </summary>
    public float Score { get; set; }

    /// <summary>
    /// How the match was achieved (e.g., 'regex', 'embedding', 'keyword').
    /// </summary>
    public MatchType MatchType { get; set; }

    /// <summary>
    /// User's language (e.g., 'en', 'no').
    /// </summary>
    public string Language { get; set; } = null!;

    /// <summary>
    /// Timestamp (UTC) when this entry was logged.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Extra metadata, flags, or JSON for advanced analytics (optional).
    /// </summary>
    public string? ExtraData { get; set; }

    /// <summary>
    /// The session identifier associated with the user’s chat session.
    /// Enables joining analytics and FAQ queries by session for cross-feature reporting.
    /// </summary>
    public string SessionId { get; set; } = null!;
}