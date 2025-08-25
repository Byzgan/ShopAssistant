namespace ShopAssistant.Contracts.Models.Chat;

using Enums;

/// <summary>
/// Represents a single semantic search result row.
/// </summary>
public class SearchResult
{
    /// <summary>
    /// Question variant identifier.
    /// </summary>
    public int QuestionId { get; set; }

    /// <summary>
    /// Text of the matched question variant.
    /// </summary>
    public string Question { get; set; } = null!;

    /// <summary>
    /// Associated knowledge item (answer) identifier.
    /// </summary>
    public int KnowledgeId { get; set; }

    /// <summary>
    /// Topic/category of the matched knowledge item.
    /// </summary>
    public KnowledgeTopic Topic { get; set; }

    /// <summary>
    /// Language code ("en", "no", etc.).
    /// </summary>
    public string Language { get; set; } = null!;

    /// <summary>
    /// Canonical answer text for this match.
    /// </summary>
    public string Answer { get; set; } = null!;

    /// <summary>
    /// Similarity score from the semantic retriever (e.g., cosine).
    /// Higher is better. Range recommendation: [0,1].
    /// </summary>
    public double Score { get; set; }
}