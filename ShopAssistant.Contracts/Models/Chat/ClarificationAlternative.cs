namespace ShopAssistant.Contracts.Models.Chat;

using System.Text.Json.Serialization;
using Enums;

/// <summary>
/// Represents a single alternative for user clarification in case of ambiguity,
/// such as ambiguous intent or slot value (e.g., category).
/// </summary>
public class ClarificationAlternative
{
    /// <summary>
    /// Name of the slot this value applies to (e.g., "Category").
    /// </summary>
    public string? SlotType { get; set; }

    /// <summary>
    /// For slot ambiguity: The value for the slot (e.g., "Tablet").
    /// </summary>
    public string? SlotValue { get; set; }

    /// <summary>
    /// Localized display label for the option.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// The relevance/confidence score for this alternative (0.0–1.0).
    /// Higher values indicate greater confidence or likelihood.
    /// </summary>
    public float Score { get; set; }

    /// <summary>
    /// The type of match.
    /// Indicates how this alternative was determined as a plausible option.
    /// </summary>
    public MatchType MatchType { get; set; }
}