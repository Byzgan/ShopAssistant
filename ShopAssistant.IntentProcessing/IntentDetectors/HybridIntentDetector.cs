namespace ShopAssistant.IntentProcessing.IntentDetectors;

using Contracts.Enums;
using Contracts.Interfaces.Intent;
using Contracts.Interfaces.TextProcessing;
using Contracts.Models.Chat;
using Contracts.Models.Intent;
using Contracts.Interfaces.Localization;
using Infrastructure.TextProcessing.Stemmers;

/// <summary>
/// Hybrid intent detector for e-commerce assistant, using shared robust pattern matcher,
/// and advanced orchestration to support ambiguity, alternatives, and all match strategies.
/// </summary>
public class HybridIntentDetector(
    ITextEmbedder embedder,
    IIntentDetectorEmbeddingsCacheService embeddingsCache,
    IIntentPatternCacheService patternCacheService,
    ILocalizationService localizationService,
    IIntentPatternMatcher patternMatcher) : IIntentDetector
{
    private const float DefaultScoreTolerance = 0.04f;
    private const string CacheScope = "intents";

    public async Task<IntentDetectionResult> DetectIntentAsync(string language, string message)
    {
        // --- Guard: empty message ---
        if (string.IsNullOrWhiteSpace(message))
        {
            return new IntentDetectionResult
            {
                Intent = Intent.Unknown,
                MatchScore = 0f,
                ExtraData = new Dictionary<string, string>(),
                Alternatives = []
            };
        }

        language = language.ToLowerInvariant();
        var stemmer = StemmerFactory.GetStemmer(language);

        // --- Load patterns for the language ---
        var patterns = patternCacheService.GetPatternsForLanguage(language);
        if (patterns is null || patterns.Count == 0)
        {
            return new IntentDetectionResult
            {
                Intent = Intent.Unknown,
                MatchScore = 0f,
                ExtraData = null,
                Alternatives = []
            };
        }

        // --- Load cached intent embeddings map (if available) ---
        embeddingsCache.TryGet(out var intentEmbeddings);

        // --- Decide whether we need a message embedding (only if any pattern has embeddings for this language) ---
        float[]? msgEmbedding = null;
        bool anyPatternHasEmbeds =
            intentEmbeddings != null &&
            patterns.Any(p =>
                intentEmbeddings.TryGetValue(p.Intent, out var byLang) &&
                byLang.TryGetValue(language, out var list) &&
                list is { Count: > 0 });

        if (anyPatternHasEmbeds)
        {
            msgEmbedding = await embedder.GetEmbeddingAsync(message);
        }

        // --- Single-pass hybrid matching (lexical + semantic fallback handled inside IntentPatternMatcher) ---
        var candidates = new List<IntentScore>();

        foreach (var pattern in patterns)
        {
            // Gather phrase embeddings for this intent+language (if any)
            List<float[]>? phraseEmbeddings = null;
            if (intentEmbeddings != null && intentEmbeddings.TryGetValue(pattern.Intent, out var byLang) && byLang.TryGetValue(language, out var list) && list is { Count: > 0 })
            {
                // filter-out any empty vectors defensively
                phraseEmbeddings = list.Where(e => e is { Length: > 0 }).ToList();
            }

            // Call the matcher ONCE per pattern; it already tries regex/keyword/partial and then semantic (if embeddings provided).
            var match = patternMatcher.Match(
                language,
                message,
                pattern,
                stemmer,
                msgEmbedding,
                phraseEmbeddings);

            if (!match.IsMatch)
                continue;

            candidates.Add(new IntentScore
            {
                Intent = pattern.Intent,
                Score = match.Score,
                MatchType = match.MatchType
            });
        }

        // --- Rank & tolerance bucket for alternatives ---
        if (candidates.Count > 0)
        {
            candidates = candidates.OrderByDescending(c => c.Score).ToList();

            var topScore = candidates[0].Score;
            var topBucket = candidates.Where(c => (topScore - c.Score) <= DefaultScoreTolerance).ToList();
            var best = topBucket[0];

            return new IntentDetectionResult
            {
                Intent = best.Intent,
                MatchScore = best.Score,
                ExtraData = IntentSpecialFlagsProvider.GetFlags(best.Intent, language, message),
                Alternatives = topBucket
                    .Select(c => new ClarificationAlternative
                    {
                        SlotType = "Intent",
                        SlotValue = c.Intent.ToString(),
                        Score = c.Score,
                        MatchType = c.MatchType,
                        DisplayName = localizationService.GetMessage(c.Intent.ToString(), language, CacheScope)
                    })
                    .ToList()
            };
        }

        // --- No match found ---
        return new IntentDetectionResult
        {
            Intent = Intent.Unknown,
            MatchScore = 0f,
            ExtraData = null,
            Alternatives = []
        };
    }
}
