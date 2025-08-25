namespace ShopAssistant.Infrastructure.KnowledgeBase;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShopAssistant.Contracts.Interfaces.KnowledgeBase;
using ShopAssistant.Contracts.Models.KnowledgeBase;
using TextProcessing.SemanticSearch.Embeddings;

/// <summary>
/// Initializes in-memory caches for fast KB lookups:
///  - (QuestionId, language) -> KnowledgeItem   (QID is the per-language id from ANN meta)
///  - (QuestionText, language) -> KnowledgeItem
/// Also runs a light post-init sanity check to ensure QID→KID cache matches the ANN mapping.
/// </summary>
public class KnowledgeCacheInitializer(IKnowledgeLoader knowledgeLoader, EmbeddingIndexCacheService embeddingIndexCache, IKnowledgeItemCacheService knowledgeItemCacheService, ILogger<KnowledgeCacheInitializer> logger)
{
    public async Task InitializeCacheAsync()
    {
        // 1) Load all KnowledgeItems once
        var knowledgeItems = await knowledgeLoader.LoadAllAsync().ConfigureAwait(false);
        if (knowledgeItems.Count == 0)
        {
            logger.LogWarning("No knowledge items found to initialize cache.");
            return;
        }

        // Build an index by (lang, KnowledgeId) for fast KID -> item resolution.
        // Language key normalized to lower-invariant to match runtime lookups.
        var itemByLangKid = knowledgeItems
            .GroupBy(ki => LangKey(ki.Language))
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(ki => ki.Id, ki => ki));

        // 2) Populate cache by (QuestionId, language) using the ANN meta mapping per language.
        //    QID MUST come from kb_meta_{lang}.json, not from the KB JSON.
        foreach (var (lang, byKid) in itemByLangKid)
        {
            var mappingStore = embeddingIndexCache.GetKnowledgeBaseMappingStore(lang);
            var qidToKid = mappingStore.QuestionAnswerMapping; // Dictionary<int QID, int KID>

            if (qidToKid.Count == 0)
            {
                logger.LogWarning("Question→Knowledge mapping is empty for language '{Lang}'. Skipping by-QuestionId cache.", lang);
                continue;
            }

            int saved = 0, missing = 0;
            foreach (var (qid, kid) in qidToKid)
            {
                if (!byKid.TryGetValue(kid, out KnowledgeItem? item))
                {
                    missing++;
                    continue;
                }

                knowledgeItemCacheService.SaveKnowledgeItemByQuestionId(qid, lang, item);

                saved++;
            }

            if (missing > 0)
                logger.LogWarning("Missing {Missing} KnowledgeItems referenced by mapping in '{Lang}'. (Stale export vs. KB?)", missing, lang);

            logger.LogInformation("Cached {Saved} (QuestionId, {Lang}) entries from ANN mapping.", saved, lang);

            // 2a) Optional sanity check: spot-check that cached (QID,lang) resolves to the mapped KID.
            //     Keeps startup fast while still catching drift.
            int mismatches = 0;
            int sampleStep = qidToKid.Count <= 50 ? 1 : qidToKid.Count / 50; // up to 50 samples
            foreach (var (qid, kid) in qidToKid.Where((_, i) => i % sampleStep == 0))
            {
                var cached = knowledgeItemCacheService.GetKnowledgeItemByQuestionId(qid, lang);
                if (cached is not null && cached.Id == kid) 
                    continue;
                
                mismatches++;
                logger.LogError("Cache mismatch for lang='{Lang}': QID={Qid} -> expected KID={Kid}, got {GotKid}", lang, qid, kid, cached?.Id);
            }

            if (mismatches == 0)
                logger.LogInformation("By-QuestionId cache matches ANN mapping for '{Lang}'.", lang);
        }

        // 3) Populate cache by (Question TEXT, language). This is independent from QIDs.
        int savedByText = 0;
        foreach (var ki in knowledgeItems)
        {
            if (ki.Questions is not { Count: > 0 }) continue;

            var lang = LangKey(ki.Language);
            foreach (var q in ki.Questions)
            {
                if (string.IsNullOrWhiteSpace(q))
                    continue;

                knowledgeItemCacheService.SaveKnowledgeItemByQuestionText(q.Trim(), lang, ki);
                savedByText++;
            }
        }

        logger.LogInformation("Knowledge items cache initialized: {TextCount} entries by question text across {ItemCount} knowledge items.", savedByText, knowledgeItems.Count);
    }

    private static string LangKey(string s) => s.Trim().ToLowerInvariant();
}
