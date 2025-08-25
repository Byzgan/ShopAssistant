using ShopAssistant.Contracts.Enums;
using ShopAssistant.Contracts.Interfaces.Intent;
using ShopAssistant.Contracts.Models.Intent;
using ShopAssistant.Infrastructure.TextProcessing.Stemmers;

namespace ShopAssistant.IntentProcessing.IntentDetectors;

/// <summary>
/// Rule-based intent detector using intent patterns and stemming for robust recognition.
/// Leverages the shared pattern matcher for detection logic.
/// </summary>
public class RuleBasedIntentDetector(IIntentPatternCacheService patternCacheService, IIntentPatternMatcher intentPatternMatcher) : IIntentDetector
{
    /// <summary>
    /// Initializes the detector (no-op for rule-based).
    /// </summary>
    public Task InitAsync() => Task.CompletedTask;

    /// <summary>
    /// Detects the intent of the user's message using pattern matching and stemming.
    /// </summary>
    /// <param name="language">Language code ("en", "no", ...).</param>
    /// <param name="message">User message text.</param>
    /// <returns>Detected intent result.</returns>
    public Task<IntentDetectionResult> DetectIntentAsync(string language, string message)
    {
        language = language.ToLowerInvariant();
        var stemmer = StemmerFactory.GetStemmer(language);

        var result = new IntentDetectionResult()
        {
            Intent = Intent.Unknown,
            ExtraData = new Dictionary<string, string>()
        };

        // Get all patterns for the language
        var patterns = patternCacheService.GetPatternsForLanguage(language);

        // Try matching each pattern using the robust matcher
        if (patterns is not null)
        {
            foreach (var pattern in patterns)
            {
                var matchResult = intentPatternMatcher.Match(language, message, pattern, stemmer);

                if (matchResult.IsMatch)
                {
                    result.Intent = pattern.Intent;
                    break;
                }
            }
        }

        // Special flag detection (e.g., "latest" order)
        if (result.Intent != Intent.Unknown)
        {
            var flags = IntentSpecialFlagsProvider.GetFlags(result.Intent, language, message);
            if (flags.Any())
                result.ExtraData = flags;
        }
        else
        {
            // If still unknown, fallback to FAQ
            result.Intent = Intent.FAQ;
        }

        return Task.FromResult(result);
    }
}
