namespace ShopAssistant.Contracts.Enums;


/// <summary>
/// Specifies the type of match to use when searching or comparing values.
/// </summary>
public enum MatchType
{
    None = 0,

    /// <summary>
    /// Exact keyword match.
    /// </summary>
    KeyWord,

    /// <summary>
    /// Fuzzy match.
    /// </summary>
    Fuzzy,

    /// <summary>
    /// Semantic match.
    /// </summary>
    Semantic
}
