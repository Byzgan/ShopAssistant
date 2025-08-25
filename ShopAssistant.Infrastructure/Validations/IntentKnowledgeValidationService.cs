namespace ShopAssistant.Infrastructure.Validations;

using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Contracts.Enums;
using Contracts.Interfaces.Intent;
using Contracts.Models.Intent;
using Contracts.Models;
using TextProcessing.Intent;
using ShopAssistant.Contracts.Interfaces.KnowledgeBase;
using ShopAssistant.Contracts.Interfaces.TextProcessing;
using ShopAssistant.Contracts.Models.Validations;

public class IntentKnowledgeValidationService(IKnowledgeLoader knowledgeLoader, ITextEmbedder embedder, IIntentPatternMatcher intentPatternMatcher, IConfiguration configuration)
{
    // Compact, language-aware “where-like” question detectors (no arrays).
    // These treat questions like “Where can I find my tracking info?” / “Hvor finner jeg … ?”
    // as KB-style informational questions rather than action intents.
    private static readonly Regex WhereLikeEn = new(@"^\s*where\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex WhereLikeNo = new(@"^\s*hvor\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Validates knowledge base vs. intent patterns across all supported languages.
    /// Steps:
    ///   1) Load patterns (per language) and KB items.
    ///   2) Precompute embeddings for KB questions and pattern phrases.
    ///   3) Run the conflict checker (lexical + semantic via matcher).
    ///   4) Apply minimal suppression for exact phrase matches and “where-like” KB questions.
    /// </summary>
    public async Task<List<ConflictResult>> ValidateIntentKnowledgeConflictsAsync()
    {
        // 1) Resolve supported languages and pattern folder.
        var supportedLanguages = configuration.GetSection("Languages:Supported").Get<string[]>() ?? [];

        var basePath = Directory.GetCurrentDirectory();
        var configDirectory = Path.Combine(basePath, "..", configuration["IntentPatternsPath"]!);

        // 2) Load patterns & knowledge (admin-time file I/O is allowed here)
        var patternsByLanguage = await IntentPatternFileLoader.LoadAllIntentPatternsAsync(configDirectory);
        var knowledgeItems = await knowledgeLoader.LoadAllAsync();

        // 3) Filter to supported languages
        patternsByLanguage = patternsByLanguage
            .Where(p => supportedLanguages.Contains(p.Key))
            .ToDictionary(p => p.Key, p => p.Value);

        knowledgeItems = knowledgeItems
            .Where(k => supportedLanguages.Contains(k.Language))
            .ToList();

        // 4) Precompute embeddings for KB inputs and pattern phrases
        var inputEmbeddings = new Dictionary<(string Language, string Question), float[]>();
        var patternEmbeddings = new Dictionary<(string Language, IntentPattern Pattern), List<float[]>>();

        foreach (var item in knowledgeItems)
        {
            foreach (var q in item.Questions)
            {
                var embedding = await embedder.GetEmbeddingAsync(q);
                inputEmbeddings[(item.Language, q)] = embedding;
            }
        }
      
        // Build embeddings in the same order as (filtered) phrases and keep counts aligned
        foreach (var (lang, patterns) in patternsByLanguage)
        {
            foreach (var pattern in patterns)
            {
                if (pattern.SemanticPhrases is null)
                    continue;

                // Filter/normalize phrases once; keep exact order; avoids empty/null entries
                var normalizedPhrases = pattern.SemanticPhrases
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim())
                    .ToList();

                // Update the in-memory pattern so pattern.SemanticPhrases.Count == embeddings.Count
                pattern.SemanticPhrases = normalizedPhrases;

                var embeddings = new List<float[]>(normalizedPhrases.Count);
                foreach (var phrase in normalizedPhrases)
                {
                    var emb = await embedder.GetEmbeddingAsync(phrase);
                    embeddings.Add(emb);
                }

                patternEmbeddings[(lang, pattern)] = embeddings;
            }
        }


        // 5) Compute raw conflicts using the existing checker
        var checker = new IntentKnowledgeConflictChecker(intentPatternMatcher);
        var conflicts = checker.CheckForConflicts(
            patternsByLanguage,
            knowledgeItems,
            inputEmbeddings,
            patternEmbeddings);

        // 6) Apply minimal suppression rules to filter out noise
        var filtered = conflicts.Where(c => !ShouldSuppress(c)).ToList();

        return filtered;
    }

    /// <summary>
    /// Returns true if the conflict should be suppressed by minimal, language-aware rules:
    ///  • Exact-ish phrase match (KeyWord) → by design an intent trigger.
    ///  • KB-style “where-like” questions (EN/NO) → keep in knowledge base, not intent.
    ///  • Very strong intent signal (Fuzzy ≥ 0.90 or Semantic ≥ 0.93), as long as the question
    ///    is NOT “where-like”, to reduce noise from duplicates/near-miss semantic overlaps.
    /// </summary>
    private static bool ShouldSuppress(ConflictResult c)
    {
        var type = c.MatchType;
        var score = c.Score;
        var lang = c.Language.ToLowerInvariant();
        var questionLower = c.Question.ToLowerInvariant();

        // (1) Exact-ish intent phrase
        if (type == MatchType.KeyWord)
            return true;

        // (2) KB “where-like” forms (English/Norwegian)
        if (IsWhereLikeQuestion(questionLower, lang))
            return true;

        // (3) Very strong non-where intent signals
        if (!IsWhereLikeQuestion(questionLower, lang) && ((type == MatchType.Fuzzy && score >= 0.90f) || (type == MatchType.Semantic && score >= 0.93f)))
            return true;

        return false;
    }

    /// <summary>
    /// Detects “where-like” interrogatives in a language-aware way via compact regex.
    /// No arrays, no topic knowledge.
    /// </summary>
    private static bool IsWhereLikeQuestion(string questionLower, string languageLower)
    {
        return languageLower == "no" 
            ? WhereLikeNo.IsMatch(questionLower)
            : WhereLikeEn.IsMatch(questionLower);
    }
}
