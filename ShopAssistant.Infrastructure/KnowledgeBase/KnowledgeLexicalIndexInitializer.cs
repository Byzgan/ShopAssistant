namespace ShopAssistant.Infrastructure.KnowledgeBase;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShopAssistant.Contracts.Interfaces.TextProcessing;
using TextProcessing.SemanticSearch.Embeddings;

/// <summary>
/// Builds the BM25 lexical index over *question variants* for each configured language,
/// using the ANN metadata preloaded into memory at startup by <see cref="EmbeddingIndexCacheService"/>.
/// </summary>
public  class KnowledgeLexicalIndexInitializer(IBm25QuestionIndex bm25Index, EmbeddingIndexCacheService embeddingIndexCache, IConfiguration configuration, ILogger<KnowledgeLexicalIndexInitializer> logger)
{
    /// <summary>
    /// Rebuilds BM25 indices for all languages listed under "Languages:Supported".
    /// Uses only memory-resident data; perform after ANN caches are warmed.
    /// </summary>
    public Task InitializeAsync(CancellationToken ct = default)
    {
        var langs = configuration.GetSection("Languages:Supported").Get<string[]>() ?? [];
        if (langs.Length == 0)
        {
            logger.LogWarning("No languages configured under 'Languages:Supported'. BM25 will not be built.");
            return Task.CompletedTask;
        }

        foreach (var rawLang in langs)
        {
            if (ct.IsCancellationRequested)
                break;

            var lang = rawLang.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lang))
                continue;

            // Clear existing BM25 data before rebuilding
            bm25Index.ClearLanguage(lang);

            try
            {
                var store = embeddingIndexCache.GetKnowledgeBaseMappingStore(lang);

                // Use the preloaded canonical question texts (QuestionId → QuestionText)
                var questionTextMap = store.QuestionTexts;

                if (questionTextMap.Count == 0)
                {
                    logger.LogWarning("No question texts available for language '{Lang}'. BM25 not built.", lang);
                    continue;
                }

                var pairs = new List<(int QuestionId, string Text)>(questionTextMap.Count);
                foreach (var (qid, qText) in questionTextMap)
                {
                    if (!string.IsNullOrWhiteSpace(qText))
                        pairs.Add((qid, qText));
                    else
                        logger.LogDebug("Empty question text for QuestionId {Qid} in '{Lang}'. Skipped.", qid, lang);
                }

                if (pairs.Count == 0)
                {
                    logger.LogWarning("No usable question variants found for language '{Lang}'. BM25 not built.", lang);
                    continue;
                }

                bm25Index.Build(lang, pairs);
                logger.LogInformation("BM25 built for language '{Lang}' with {Count} question variants.", lang, pairs.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to build BM25 for language '{Lang}'.", lang);
            }
        }

        return Task.CompletedTask;
    }
}
