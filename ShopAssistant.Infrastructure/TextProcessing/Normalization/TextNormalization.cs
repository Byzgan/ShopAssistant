namespace ShopAssistant.Infrastructure.TextProcessing.Normalization;

using System.Text.RegularExpressions;

/// <summary>
/// Centralized language-aware text normalization and tokenization utilities,
/// used by both intent matching and lexical (BM25) retrieval to guarantee
/// identical behavior across components.
/// </summary>
public static class TextNormalization
{
    /// <summary>
    /// Language-specific regex to strip punctuation/characters that should not
    /// participate in lexical matching. Keep this map authoritative so all
    /// subsystems (intents, BM25, etc.) behave identically.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> LanguageRegexMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "no", @"[^\wæøåÆØÅ\s'\-]" },
            { "sv", @"[^\wåäöÅÄÖ\s'\-]" },
            { "da", @"[^\wæøåÆØÅ\s'\-]" },
            { "en", @"[^\w\s'\-]" },
            { "fr", @"[^\p{L}0-9\s'\-]" },
            { "de", @"[^\p{L}0-9\s'\-]" },
            { "es", @"[^\p{L}0-9\s'\-¿¡]" },
            { "it", @"[^\p{L}0-9\s'\-]" },
            { "pt", @"[^\p{L}0-9\s'\-]" }
        };

    /// <summary>
    /// Lowercase + language-specific cleanup. Use this when you need a normalized
    /// string but not the token array.
    /// </summary>
    public static string NormalizeLower(string language, string text)
    {
        text = string.IsNullOrWhiteSpace(text) ? string.Empty : text;
        var lang = language.ToLowerInvariant();
        var rx = LanguageRegexMap.GetValueOrDefault(lang, @"[^\p{L}0-9\s'\-]");
        var lowered = text.ToLowerInvariant();

        return Regex.Replace(lowered, rx, "");
    }

    /// <summary>
    /// Lowercase + cleanup + whitespace split. Optionally de-duplicate tokens
    /// while preserving the first-seen order (useful for query term lists).
    /// </summary>
    /// <param name="language">Language code (e.g., "en", "no").</param>
    /// <param name="text">Input text.</param>
    /// <param name="deduplicate">If true, removes duplicate tokens keeping first appearance.</param>
    public static string[] Tokenize(string language, string text, bool deduplicate = false)
    {
        var normalized = NormalizeLower(language, text);
        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (!deduplicate) 
            return parts;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unique = new List<string>(parts.Length);
        foreach (var p in parts)
        {
            if (seen.Add(p))
                unique.Add(p);
        }
        return unique.ToArray();
    }

    public static string NormalizeSpaces(string s) => Regex.Replace(s, @"\s+", " ").Trim();

    public static string NormalizeLowerTrimEndPunct(string s)
    {
        s = NormalizeSpaces(s);
        s = Regex.Replace(s, @"[\.!\?,]+$", ""); // remove only at end

        return s.ToLowerInvariant();
    }
}
