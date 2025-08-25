namespace ShopAssistant.Contracts.Models.Intent;

using Enums;

/// <summary>
/// Immutable DTO representing the result of matching a text to an intent pattern.
/// </summary>
public record IntentPatternMatchResult(
    bool IsMatch,
    MatchType MatchType,
    float Score,
    string? MatchedPhrase = null);
