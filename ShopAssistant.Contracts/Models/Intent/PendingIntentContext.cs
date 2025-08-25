namespace ShopAssistant.Contracts.Models.Intent;

using Enums;
using System.Collections.Generic;

/// <summary>
/// Stores the state of an ongoing multi-turn (wizard-style) dialog for a user,
/// including the active intent, collected slot data, and current field prompt.
/// Used for managing dialog progress, slot-filling, and scenario flags.
/// </summary>
public class PendingIntentContext
{
    /// <summary>
    /// The intent being processed (e.g., Recommend, ProductSearch).
    /// </summary>
    public Intent Intent { get; set; }

    /// <summary>
    /// The name of the current slot/field being prompted (e.g., "Category", "Budget").
    /// </summary>
    public string CurrentField { get; set; } = string.Empty;

    /// <summary>
    /// The localized prompt/message to show the user for the current field.
    /// </summary>
    public string CurrentPrompt { get; set; } = string.Empty;

    /// <summary>
    /// The values collected so far for required slots (slot/field name to user answer).
    /// </summary>
    public Dictionary<string, string> CollectedData { get; set; } = new();
}