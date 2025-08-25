namespace ShopAssistant.Contracts.Models.Analytics;

/// <summary>
/// DTO representing a customer question that was not answered from the knowledge base, FAQ, or automated assistant.
/// Used for shop-owner dashboard analytics.
/// </summary>
public class UnansweredQuestion
{
    /// <summary>
    /// Unique record ID for the unanswered question (from UnansweredQuestions table).
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The text of the customer question that was not answered.
    /// </summary>
    public string InputText { get; set; } = null!;

    /// <summary>
    /// The user identifier who asked the question.
    /// </summary>
    public string UserId { get; set; } = null!;

    /// <summary>
    /// The external system/shop/channel where the question was asked.
    /// </summary>
    public string ExternalSystem { get; set; } = null!;

    /// <summary>
    /// Date and time when the question was asked (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The language code of the question (e.g., "en", "ru").
    /// </summary>
    public string Language { get; set; } = null!;
}