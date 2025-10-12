namespace ShopAssistant.Infrastructure.TextProcessing.SemanticSearch;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Contracts.Enums;
using Embeddings;
using ShopAssistant.Contracts.Interfaces.KnowledgeBase;
using ShopAssistant.Contracts.Interfaces.TextProcessing;
using ShopAssistant.Contracts.Models.Chat;
using ShopAssistant.Contracts.Models.KnowledgeBase;


/// <summary>
/// ANN-based semantic retrieval (HNSW with cosine distance normalized to [0,1]).
/// </summary>
public class SemanticSearchService(EmbeddingIndexCacheService indexCache, IKnowledgeItemCacheService knowledgeCache, ILogger<SemanticSearchService> logger) : ISemanticSearchService
{
    // Threshold shaped relative to the top result; clamp to avoid being too strict or too lax.
    private const double ThresholdCeiling = 0.85;
    private const double ThresholdFloor = 0.65;
    private const double TopMargin = 0.04;

    // Candidate sizing knobs.
    private const int ReserveCandidates = 5;
    private const int RequestedNearestNeighbors = 100; // wider than before to reduce early pruning errors
    private const int MinimumShortlistWidth = 50; // ensure enough distinct KIDs for re-ranking

    /// <inheritdoc />
    public async Task<List<SearchResult>> SemanticSearchAsync(float[] embedding, string language, HashSet<KnowledgeTopic>? allowedTopics, int topK = 1)
    {
        if (embedding is null || embedding.Length == 0)
            throw new ArgumentException("Embedding must be a non-empty vector.", nameof(embedding));
        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("Language must be provided.", nameof(language));
        if (topK <= 0) 
            topK = 1;

        var langKey = language.Trim().ToLowerInvariant();

        // ANN index + QID->KID mapping
        var vectorStore = indexCache.GetKnowledgeBaseVectorStore(langKey);
        var mappingStore = indexCache.GetKnowledgeBaseMappingStore(langKey);
        var qidToKid = mappingStore.QuestionAnswerMapping; // QID -> KID

        if (vectorStore.Count != qidToKid.Count)
            logger.LogWarning("ANN/mapping size mismatch for '{Lang}': index={IndexCount}, mapping={MapCount}.", langKey, vectorStore.Count, qidToKid.Count);
        
        // ANN search
        IReadOnlyList<VectorSearchResult<(int Id, float)>> nearestResults;
        try
        {
            nearestResults = vectorStore.FindNearest((0, embedding), RequestedNearestNeighbors);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Vector search failed for language '{Language}' (topK={TopK}).", langKey, topK);
            return [];
        }

        if (nearestResults.Count == 0) return [];

        // Sort ANN by similarity and keep a generous shortlist
        var shortlistWidth = Math.Max(topK + ReserveCandidates, MinimumShortlistWidth);
        var hits = nearestResults
            .OrderByDescending(r => r.Score ?? 0.0)
            .Take(shortlistWidth)
            .ToList();

        var topScore = hits[0].Score ?? 0.0;
        var adaptiveThreshold = Math.Clamp(topScore - TopMargin, ThresholdFloor, ThresholdCeiling);
        
        logger.LogDebug("Semantic filter: topScore={Top:F3}, threshold={Thr:F3}, topK={TopK}, lang={Lang}", topScore, adaptiveThreshold, topK, langKey);

        // Best QID per KID BEFORE thresholding
        List<(int Kid, int Qid, double Score, float Dist)> orderedBestPerKid;
        {
            var bestPerKid = new Dictionary<int, (int Qid, double Score, float Dist)>(hits.Count);
            foreach (var h in hits)
            {
                var qid = h.Record.Item1;
                var sc = h.Score ?? 0.0;
                var dist = h.Record.Item2;

                if (!qidToKid.TryGetValue(qid, out var kid))
                    continue;

                if (!bestPerKid.TryGetValue(kid, out var cur) || sc > cur.Score)
                    bestPerKid[kid] = (qid, sc, dist);
            }

            orderedBestPerKid = bestPerKid
                .Select(kvp => (Kid: kvp.Key, kvp.Value.Qid, kvp.Value.Score, kvp.Value.Dist))
                .OrderByDescending(x => x.Score)
                .ToList();
        }

        // Apply threshold
        var candidates = orderedBestPerKid
            .Where(x => x.Score >= adaptiveThreshold)
            .Select(x => (x.Qid, x.Kid, x.Score))
            .ToList();

        // …but always top up to at least topK distinct KIDs by adding next-best (even if below threshold)
        if (candidates.Count < topK)
        {
            foreach (var extra in orderedBestPerKid)
            {
                if (candidates.Count >= topK) 
                    break;
                if (candidates.Any(c => c.Kid == extra.Kid)) 
                    continue;
                
                candidates.Add((extra.Qid, extra.Kid, extra.Score));
            }
        }

        // Resolve KnowledgeItems and build final results
        var resolved = await Task.WhenAll(candidates.Select(pair =>
        {
            try
            {
                var item = knowledgeCache.GetKnowledgeItemByQuestionId(pair.Qid, langKey);
                return Task.FromResult((pair.Qid, pair.Kid, item, pair.Score));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while resolving KnowledgeItem for QID={Qid}.", pair.Qid);
                return Task.FromResult((pair.Qid, pair.Kid, (KnowledgeItem?)null, pair.Score));
            }
        })).ConfigureAwait(false);

        var results = new List<SearchResult>(resolved.Length);
        foreach (var (qid, kid, item, score) in resolved)
        {
            if (item is null || item.Id <= 0 || string.IsNullOrWhiteSpace(item.Answer)) continue;

            if (item.Id != kid)
                logger.LogWarning("KnowledgeId mismatch for QID={Qid}: mapping={MapKid}, item.Id={ItemKid}", qid, kid, item.Id);

            if (allowedTopics is null || allowedTopics.Contains(item.Topic))
            {
                results.Add(new SearchResult
                {
                    QuestionId = qid,
                    KnowledgeId = item.Id,
                    Topic = item.Topic,
                    Answer = item.Answer,
                    Language = item.Language,
                    Score = score // ANN similarity
                });
            }
        }

        return results
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SearchResult?> GetBestSemanticMatchAsync(float[] embedding, string language, HashSet<KnowledgeTopic>? allowedTopics)
    {
        var candidates = await SemanticSearchAsync(embedding, language, allowedTopics).ConfigureAwait(false);
        return candidates.FirstOrDefault();
    }
}
