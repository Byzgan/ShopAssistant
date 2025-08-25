namespace ShopAssistant.Contracts.Models.Intent;

using Chat;
using Enums;

/// <summary>
/// User intent detection for scenario control and ambiguity resolution.
/// </summary>
public class IntentDetectionResult
{
    /// <summary>
    /// The detected user intent.
    /// </summary>
    public Intent Intent { get; set; }

    /// <summary>
    /// The match score (e.g., semantic similarity or rule hit).
    /// </summary>
    public float MatchScore { get; set; } = 1.0f;

    /// <summary>
    /// Optional extra data for scenario control (e.g., "Latest" = "true" for "latest order").
    /// Used to pass flags, parameters or scenario keys to the handler/multi-turn logic.
    /// </summary>
    public Dictionary<string, string>? ExtraData { get; set; }

    /// <summary>
    /// Type of ambiguity for clarification (Intent, Category, ...), if ambiguity exists.
    /// </summary>
    public ClarificationType? ClarificationType { get; set; }

    /// <summary>
    /// All plausible alternatives for ambiguity handling (intent or slot/category).
    /// If only one intent/slot was found, this will be null or a single element matching <see cref="Intent"/>.
    /// </summary>
    public List<ClarificationAlternative>? Alternatives { get; set; }

    /// <summary>
    /// Optional clarification prompt (localized) for the ambiguity.
    /// </summary>
    public string? ClarificationPrompt { get; set; }
}
