namespace ShopAssistant.IntentProcessing.IntentDetectors;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Contracts.Enums;
using Contracts.Interfaces.Intent;
using Contracts.Interfaces.TextProcessing;

/// <summary>
/// Responsible for initializing (and force-reloading) the intent embeddings cache.
/// Should be called at startup and after any intent pattern change.
/// </summary>
public class IntentEmbeddingCacheInitializer(ITextEmbedder embedder, IIntentDetectorEmbeddingsCacheService cacheService, IIntentPatternCacheService patternCacheService, IConfiguration configuration, ILogger<IntentEmbeddingCacheInitializer> logger)
{
    /// <summary>
    /// Initializes and populates the intent embeddings cache.
    /// </summary>
    public async Task InitializeCacheAsync()
    {
        logger.LogInformation("Building intent embeddings cache at application startup...");

        var languages = configuration.GetSection("Languages:Supported").Get<string[]>() ?? [];
        if (languages.Length == 0)
        {
            logger.LogError("No languages specified in configuration for embeddings initialization.");
            return;
        }

        var intentEmbeddings = new Dictionary<Intent, Dictionary<string, List<float[]>>>();

        foreach (var language in languages)
        {
            var patterns = patternCacheService.GetPatternsForLanguage(language);
            if (patterns == null || patterns.Count == 0)
                continue;

            foreach (var pattern in patterns)
            {
                if (pattern.SemanticPhrases is null or { Count: 0 }) 
                    continue;

                var embeddings = new List<float[]>(pattern.SemanticPhrases.Count);
                foreach (var phrase in pattern.SemanticPhrases)
                {
                    try
                    {
                        var embedding = await embedder.GetEmbeddingAsync(phrase);
                        embeddings.Add(embedding);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to embed phrase '{Phrase}' for intent '{Intent}' in lang '{Lang}'", phrase, pattern.Intent, language);
                    }
                }

                if (!intentEmbeddings.TryGetValue(pattern.Intent, out var langDict))
                {
                    langDict = new Dictionary<string, List<float[]>>();
                    intentEmbeddings[pattern.Intent] = langDict;
                }
                langDict[language] = embeddings;
            }
        }

        cacheService.Set(intentEmbeddings);

        logger.LogInformation("Intent embeddings cache built and stored in memory.");
    }
}
