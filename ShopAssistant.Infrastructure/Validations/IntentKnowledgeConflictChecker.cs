namespace ShopAssistant.Infrastructure.Validations;

using Contracts.Interfaces.Intent;
using Contracts.Models;
using Contracts.Models.Intent;
using ShopAssistant.Contracts.Models.KnowledgeBase;
using ShopAssistant.Contracts.Models.Validations;
using TextProcessing.Stemmers;

/// <summary>
/// Checks for conflicts between intent patterns and KB questions using the exact same logic
/// as used for production intent detection (HybridIntentDetector/IntentPatternMatcher).
/// Must be called from a project that references Infrastructure.TextProcessing.
/// </summary>
public class IntentKnowledgeConflictChecker(IIntentPatternMatcher intentPatternMatcher)
{
    /// <summary>
    /// Checks all KB questions for matches with all intent patterns (per language),
    /// using negative, regex, partial keyword, and semantic checks.
    /// For semantic checks, provide precomputed embeddings (as in runtime cache).
    /// </summary>
    /// <param name="intentPatternsByLanguage">Intent patterns by language.</param>
    /// <param name="knowledgeItems">Knowledge base entries to check.</param>
    /// <param name="inputEmbeddings">Precomputed embeddings for KB questions (optional, for semantic check).</param>
    /// <param name="patternEmbeddings">Precomputed embeddings for pattern phrases (optional, for semantic check).</param>
    /// <returns>All detected conflicts with full details.</returns>
    public List<ConflictResult> CheckForConflicts(
        Dictionary<string, List<IntentPattern>> intentPatternsByLanguage,
        IEnumerable<KnowledgeItem> knowledgeItems,
        Dictionary<(string Language, string Question), float[]>? inputEmbeddings = null,
        Dictionary<(string Language, IntentPattern Pattern), List<float[]>>? patternEmbeddings = null)
    {
        var conflicts = new List<ConflictResult>();
        var knowledgeByLanguage = knowledgeItems
            .GroupBy(k => k.Language)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (language, patterns) in intentPatternsByLanguage)
        {
            if (!knowledgeByLanguage.TryGetValue(language, out var knowledgeInLanguage))
                continue;

            foreach (var knowledgeItem in knowledgeInLanguage)
            {
                foreach (var question in knowledgeItem.Questions)
                {
                    // Get the correct stemmer for this language
                    var stemmer = StemmerFactory.GetStemmer(language);

                    foreach (var pattern in patterns)
                    {
                        float[]? inputEmbedding = null;
                        List<float[]>? phraseEmbeddings = null;
                        inputEmbeddings?.TryGetValue((language, question), out inputEmbedding);
                        patternEmbeddings?.TryGetValue((language, pattern), out phraseEmbeddings);

                        var result = intentPatternMatcher.Match(
                            language,
                            question,
                            pattern,
                            stemmer,
                            inputEmbedding,
                            phraseEmbeddings
                        );

                        if (result.IsMatch)
                        {
                            conflicts.Add(new ConflictResult(
                                language,
                                knowledgeItem,
                                pattern,
                                question,
                                result.MatchType,
                                result.Score,
                                result.MatchedPhrase
                            ));
                        }
                    }
                }
            }
        }
        return conflicts;
    }
}
