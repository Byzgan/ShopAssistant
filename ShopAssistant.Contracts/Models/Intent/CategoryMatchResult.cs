namespace ShopAssistant.Contracts.Models.Intent;

using Enums;

/// <summary>
/// Represents the result of attempting to match a category, including the canonical category, ambiguity status, and a list of close matches with their match types.
/// </summary>
public class CategoryMatchResult
{
    /// <summary>
    /// Gets or sets the canonical category if a confident match was found; otherwise, null.
    /// </summary>
    public string? CanonicalCategory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the category match is ambiguous.
    /// </summary>
    public bool IsAmbiguous { get; set; }

    /// <summary>
    /// Gets or sets a list of close category matches and their corresponding match types.
    /// </summary>
    public List<(string category, MatchType matchType)> CloseMatches { get; set; } = [];
}