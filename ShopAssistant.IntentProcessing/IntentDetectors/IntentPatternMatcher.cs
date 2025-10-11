namespace ShopAssistant.IntentProcessing.IntentDetectors;

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Contracts.Enums;
using Contracts.Interfaces.Intent;
using Contracts.Interfaces.TextProcessing;
using Contracts.Models.Intent;
using Infrastructure.TextProcessing.Normalization;

/// <summary>
/// Robust, hybrid pattern matcher used by both admin validation and runtime intent detection.
/// Order of checks:
/// 1) Hard negatives (forbidden tokens / negative phrases)
/// 2) Exact/regex phrase equality (fast path)
/// 3) Partial keyword overlap (fuzzy)
/// 4) Semantic fallback (embeddings)
/// 
/// IMPORTANT:
/// - "RequiredTokens" are modeled as groups (OR inside a group, AND across groups).
///   These are enforced for ALL positive match types, including SEMANTIC, to avoid topic drift from KB questions into intents.
/// - The global floor is used when the pattern does not specify a threshold.
/// </summary>
public sealed class IntentPatternMatcher : IIntentPatternMatcher
{
    // Tunables (conservative by default)
    private const float GlobalSemanticFloor = 0.92f;   // Global minimum cosine similarity if pattern threshold is missing
    private const double DefaultPartialKeywordCoverage = 0.6;
    private const float KeywordMatchMinScore = 0.55f;
    private const float KeywordMatchMaxScore = 0.90f;

    // Cache compiled regex per-language+phrase
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new();

    /// <summary>
    /// Matches input against a pattern using negative filtering, exact/regex, fuzzy tokens and semantic similarity.
    /// </summary>
    public IntentPatternMatchResult Match(string language, string message, IntentPattern pattern, IStemmer stemmer, float[]? inputEmbedding = null, List<float[]>? patternEmbeddings = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return new IntentPatternMatchResult(false, MatchType.None, 0f);

        language = language.ToLowerInvariant();

        // Normalize message (two views)
        string lowered = TextNormalization.NormalizeLower(language, message);
        string normalizedStem = NormalizeAndStem(lowered, stemmer);

        // Build token set once (for quick membership checks)
        HashSet<string> msgTokens = Regex.Split(normalizedStem, @"\W+")
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToHashSet();

        // Evaluate "required token groups" upfront; this is a SOFT gate:
        // - If no groups provided => treat as satisfied (backward-compatible).
        // - If groups exist => each group must be satisfied (any-of within group).
        bool anchorsSatisfied = AreRequiredGroupsSatisfied(language, stemmer, pattern.RequiredTokens, lowered, msgTokens);

        // Hard negatives
        // Forbidden tokens (explicit block)
        if (pattern.ForbiddenTokens is { Count: > 0 })
        {
            foreach (var forb in pattern.ForbiddenTokens.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                if (lowered.Contains(forb, StringComparison.OrdinalIgnoreCase) || normalizedStem.Contains(forb, StringComparison.OrdinalIgnoreCase))
                    return new IntentPatternMatchResult(false, MatchType.None, 0f);
            }
        }

        // Build tokens for the normalized, lowered user text once.
        List<string> textTokens = TextNormalization
            .Tokenize(language, lowered, deduplicate: false)   // keep order; we'll scan for sequences
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        HashSet<string> textTokenSet = new HashSet<string>(textTokens, StringComparer.Ordinal); // for fast single-word checks

        if (pattern.NegativePhrases is { Count: > 0 })
        {
            foreach (string neg in pattern.NegativePhrases.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                string negNorm = TextNormalization.NormalizeLower(language, neg);
                if (string.IsNullOrWhiteSpace(negNorm))
                    continue;

                List<string> negTokens = TextNormalization
                    .Tokenize(language, negNorm, deduplicate: false)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();

                if (negTokens.Count == 0)
                    continue;

                // Single-word negative: require exact token match (whole word).
                if (negTokens.Count == 1)
                {
                    if (textTokenSet.Contains(negTokens[0]))
                        return new IntentPatternMatchResult(false, MatchType.None, 0f);
                    continue;
                }

                // Multi-word negative: require exact subsequence match across tokens (whole phrase).
                if (ContainsTokenSubsequence(textTokens, negTokens))
                    return new IntentPatternMatchResult(false, MatchType.None, 0f);
            }
        }

        // Exact/regex phrase equality
        // Fast path: raw/space-normalized/lower-with-trailing-punct-ignored equality
        if (pattern.SemanticPhrases is { Count: > 0 })
        {
            string rawTrim = message.Trim();
            string rawNorm = TextNormalization.NormalizeSpaces(rawTrim);
            string softLower = TextNormalization.NormalizeLowerTrimEndPunct(rawTrim);

            foreach (string phrase in pattern.SemanticPhrases)
            {
                if (string.IsNullOrWhiteSpace(phrase))
                    continue;
                string pTrim = phrase.Trim();

                bool isDirectEqual =
                    string.Equals(rawTrim, pTrim, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(rawNorm, TextNormalization.NormalizeSpaces(pTrim), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(softLower, TextNormalization.NormalizeLowerTrimEndPunct(pTrim), StringComparison.Ordinal);

                if (!isDirectEqual) 
                    continue;
                if (anchorsSatisfied)
                    return new IntentPatternMatchResult(true, MatchType.KeyWord, 1.0f, phrase);
            }
        }

        // Regex phrasing (treat regex-like as regex, otherwise anchor loosely around the phrase)
        if (pattern.SemanticPhrases is { Count: > 0 })
        {
            foreach (var phrase in pattern.SemanticPhrases)
            {
                if (string.IsNullOrWhiteSpace(phrase))
                    continue;

                bool looksLikeRegex = Regex.IsMatch(phrase, @"[\[\]\(\)\{\}\.\*\+\?\|\^\$]");
                string rxSource = looksLikeRegex
                    ? phrase
                    : @"^\s*" + Regex.Escape(phrase.Trim()) + @"\s*$";

                string cacheKey = $"{language}::{phrase}";
                Regex rx = RegexCache.GetOrAdd(cacheKey, _ => new Regex(rxSource, RegexOptions.IgnoreCase | RegexOptions.Compiled));

                if (!rx.IsMatch(message) && !rx.IsMatch(lowered)) 
                    continue;
                if (anchorsSatisfied)
                    return new IntentPatternMatchResult(true, MatchType.KeyWord, 1.0f, phrase);
            }
        }

        // Partial keyword coverage (token overlap vs. each phrase)
        if (pattern.SemanticPhrases is { Count: > 0 })
        {
            foreach (var phrase in pattern.SemanticPhrases)
            {
                if (string.IsNullOrWhiteSpace(phrase)) 
                    continue;

                // Skip if contains regex-specific chars; handled above
                if (phrase.IndexOfAny(['\\', '[', ']', '(', ')', '{', '}', '.', '*', '+', '?', '|', '^', '$']) != -1)
                    continue;

                string phraseLower = TextNormalization.NormalizeLower(language, phrase);
                List<string> patternTokens = Regex.Split(phraseLower, @"\W+")
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(stemmer.Stem)
                    .ToList();

                if (patternTokens.Count == 0)
                    continue;

                int matched = patternTokens.Count(t => msgTokens.Contains(t));
                double ratio = (double)matched / patternTokens.Count;

                double need = patternTokens.Count <= 2
                    ? 1.0
                    : (pattern.PartialKeywordCoverage ?? DefaultPartialKeywordCoverage);

                if (ratio < need)
                    continue;

                float score = KeywordMatchMinScore + (float)ratio * (KeywordMatchMaxScore - KeywordMatchMinScore);
                if (score >= KeywordMatchMaxScore && anchorsSatisfied)
                    return new IntentPatternMatchResult(true, MatchType.Fuzzy, score, phrase);
            }
        }

        // Semantic fallback (embeddings)
        if (inputEmbedding != null && patternEmbeddings is { Count: > 0 } && anchorsSatisfied)
        {
            float maxScore = 0f;

            foreach (var phraseEmbedding in patternEmbeddings)
            {
                if (phraseEmbedding.Length == 0)
                    continue;

                // NOTE:
                // - We rely on consistent dimensions (same embedder everywhere).
                // - Cosine similarity returns [-1, 1]; we threshold on the raw value.
                float sim = CosineSimilarity(inputEmbedding, phraseEmbedding);
                if (sim > maxScore)
                    maxScore = sim;
            }

            // IMPORTANT:
            // The pattern threshold is used when provided; otherwise, we fall back to the global floor.
            float threshold = pattern.EmbeddingThreshold ?? GlobalSemanticFloor;

            if (maxScore >= threshold)
                return new IntentPatternMatchResult(true, MatchType.Semantic, maxScore);
        }

        // No match
        return new IntentPatternMatchResult(false, MatchType.None, 0f);
    }

    // Helpers

    /// <summary>
    /// Normalizes and stems a lower-cased string.
    /// </summary>
    private static string NormalizeAndStem(string lowered, IStemmer stemmer)
    {
        if (string.IsNullOrWhiteSpace(lowered))
            return string.Empty;

        IEnumerable<string> tokens = Regex.Split(lowered, @"\W+")
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(stemmer.Stem);

        return string.Join(' ', tokens);
    }

    /// <summary>
    /// Returns true if 'needle' appears as an exact, contiguous subsequence of 'haystack'.
    /// Comparison is Ordinal because both sides are already normalized/lowercased.
    /// </summary>
    static bool ContainsTokenSubsequence(IReadOnlyList<string> haystack, IReadOnlyList<string> needle)
    {
        if (needle.Count == 0 || haystack.Count < needle.Count)
            return false;

        for (int i = 0; i <= haystack.Count - needle.Count; i++)
        {
            bool allEqual = true;
            for (int j = 0; j < needle.Count; j++)
            {
                if (string.Equals(haystack[i + j], needle[j], StringComparison.Ordinal)) 
                    continue;
                allEqual = false;
                break;
            }
            if (allEqual) 
                return true;
        }
        return false;
    }

    /// <summary>
    /// Computes cosine similarity between two float vectors.
    /// Returns a value in [-1, 1], where 1 is identical (same direction), 0 is orthogonal, -1 is opposite.
    /// </summary>
    public static float CosineSimilarity(float[] v1, float[] v2)
    {
        if (v1 == null || v2 == null)
            throw new ArgumentNullException();
        if (v1.Length != v2.Length)
            throw new ArgumentException("Vectors must have the same length.");

        // Use double for accumulation to reduce rounding error.
        double dot = 0d, n1 = 0d, n2 = 0d;

        for (int i = 0; i < v1.Length; i++)
        {
            double a = v1[i];
            double b = v2[i];
            dot += a * b;
            n1 += a * a;
            n2 += b * b;
        }

        double denom = Math.Sqrt(n1) * Math.Sqrt(n2);
        if (denom == 0d)
            return 0f; // one (or both) vectors are all zeros

        double sim = dot / denom;

        // Clamp to [-1, 1] to counter tiny floating-point drift.
        if (sim > 1d) 
            sim = 1d;
        if (sim < -1d) 
            sim = -1d;

        return (float)sim;
    }

    /// <summary>
    /// Verifies "required token groups" (OR inside a group, AND across groups).
    /// - If groups are null/empty: returns true (backward compatible).
    /// - A group is satisfied if ANY of its anchors are present.
    /// - An anchor is considered present if:
    ///     a) For multi-word anchors: the lower-cased phrase appears with word boundaries, OR
    ///     b) ALL of its (stemmed) tokens exist in msgTokens (whole-token match).
    /// </summary>
    private static bool AreRequiredGroupsSatisfied(string language, IStemmer stemmer, List<List<string>>? groups, string loweredMessage, HashSet<string> msgTokens)
    {
        if (groups is null || groups.Count == 0)
            return true;

        foreach (var group in groups)
        {
            if (group.Count == 0)
                return false; // malformed group => fail safe

            bool anyAnchorHit = false;

            foreach (var anchor in group)
            {
                if (string.IsNullOrWhiteSpace(anchor)) continue;

                // Lower-case and normalize the anchor phrase for phrase-contains checks.
                string aNorm = TextNormalization.NormalizeLower(language, anchor).Trim();

                // Tokenize the normalized anchor (for phrase regex) and stem it (for token presence).
                List<string> aNormTokens = Regex.Split(aNorm, @"\W+")
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();

                List<string> aStemmedTokens = aNormTokens
                    .Select(stemmer.Stem)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();

                // b) ALL anchor tokens (stemmed) must be present in the message tokens.
                //    This enforces whole-token semantics and works across inflections.
                bool allTokensPresent = aStemmedTokens.Count > 0 && aStemmedTokens.All(msgTokens.Contains);

                // a) Phrase containment ONLY for multi-word anchors, with word boundaries.
                //    This prevents single words like "hvor" from matching inside "hvordan".
                bool phraseContained = false;

                if (aNormTokens.Count >= 2)
                {
                    // Build a word-bounded, whitespace-tolerant regex from the normalized tokens.
                    // Example: tokens ["endre","adresse"] -> pattern: \bendre\s+adresse\b
                    string phrasePattern = $@"\b{string.Join(@"\s+", aNormTokens.Select(Regex.Escape))}\b";

                    // Compile with CultureInvariant; the input is already lower-cased.
                    var rx = new Regex(phrasePattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);

                    phraseContained = rx.IsMatch(loweredMessage);
                }

                if (!phraseContained && !allTokensPresent) 
                    continue;

                anyAnchorHit = true;
                break;
            }

            if (!anyAnchorHit)
                return false; // this group failed => anchors not satisfied
        }

        return true; // all groups satisfied
    }
}
