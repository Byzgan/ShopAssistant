namespace ShopAssistant.Contracts.Models.Chat;

/// <summary>
/// Model for chat requests (user question).
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// The user's message to the assistant.
    /// </summary>
    public string Message { get; set; } = null!;

    /// <summary>
    /// The language code (e.g., "en", "no", "sv").
    /// </summary>
    public required string Language { get; set; }

    /// <summary>
    /// User's clarification in response to an ambiguity prompt, if applicable.
    /// If set, indicates which intent, category, or other slot value the user has selected.
    /// </summary>
    public UserClarification? UserClarification { get; set; }
}