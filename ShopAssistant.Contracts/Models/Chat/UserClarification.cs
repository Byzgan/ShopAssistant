using ShopAssistant.Contracts.Enums;

namespace ShopAssistant.Contracts.Models.Chat;

/// <summary>
/// Represents the user's explicit selection in response to an ambiguity clarification prompt,
/// such as selecting an intent or a slot value (e.g., product category).
/// </summary>
public class UserClarification
{
    /// <summary>
    /// The type of ambiguity being clarified (e.g., Intent, Category).
    /// </summary>
    public ClarificationType Type { get; set; }

    /// <summary>
    /// The value selected by the user to resolve the ambiguity.
    /// For example: intent name ("Recommend") or slot value ("Tablet").
    /// </summary>
    public string Value { get; set; } = null!;
}