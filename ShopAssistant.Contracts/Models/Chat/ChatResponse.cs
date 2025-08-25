using System.Text.Json.Serialization;
using ShopAssistant.Contracts.Enums;

namespace ShopAssistant.Contracts.Models.Chat;

/// <summary>
/// Represents the response from the chat assistant, which can be an answer, ambiguity prompt, etc.
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// Main answer to show to the user (text or HTML).
    /// </summary>
    public string Answer { get; set; } = null!;
    
    /// <summary>
    /// If true, the response is a clarification prompt (intent, category, etc).
    /// </summary>
    public bool? IsClarification { get; set; }

    /// <summary>
    /// Which type of ambiguity (Intent, Category, etc).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ClarificationType? ClarificationType { get; set; }

    /// <summary>
    /// Alternatives the user can choose to resolve ambiguity.
    /// </summary>
    public List<ClarificationAlternative>? Alternatives { get; set; }

    /// <summary>
    /// Optional prompt to display above alternatives.
    /// </summary>
    public string? ClarificationPrompt { get; set; }
}