namespace ShopAssistant.Infrastructure.KnowledgeBase;

using Contracts.Enums;
using Helpers;
using Microsoft.Extensions.Logging;
using ShopAssistant.Contracts.Interfaces.KnowledgeBase;
using ShopAssistant.Contracts.Interfaces.TextProcessing;
using ShopAssistant.Contracts.Models.Chat;
using TextProcessing.SemanticSearch.Embeddings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Hybrid retrieval:
///  1) Clean the query (same preprocessor as export), embed, get semantic shortlist (distinct KIDs).
///  2) Run BM25 on the cleaned query; map QID→KID using exported mapping.
///  3) Fuse at KID level: final = α·semantic_similarity + β·normalized_bm25_score.
///  4) If a fused-top KID lacks a row (BM25-only), materialize it on demand.
///  5) FINAL SELECTION: apply a generic lexical sanity-guard — if the top fused KID has no
///     token overlap with the query but another candidate does and is close (within a margin),
///     prefer the overlapping candidate. No hard-coded keywords.
/// </summary>
public class HybridKnowledgeBaseQueryService(
    ISemanticSearchService semanticSearch,
    ITextEmbedder embedder,
    IBm25QuestionIndex bm25Index,
    IKnowledgeItemCacheService knowledgeItemCache,
    EmbeddingIndexCacheService indexCache,
    ILogger<HybridKnowledgeBaseQueryService> logger
) : IKnowledgeBaseQueryService
{
    // Candidate widths
    private const int SemanticTopK = 30;
    private const int Bm25TopK = 50;

    // Linear fusion weights (semantic dominates overall)
    private const double AlphaSemantic = 0.70; // ANN similarity ∈ [0,1]
    private const double BetaBm25 = 0.50; // normalized BM25 ∈ [0,1]

    // Fallback if BM25 scores are unavailable: light RRF
    private const double RrfK = 10.0;
    private const double RrfSemanticWeight = 1.0;
    private const double RrfLexicalWeight = 0.50;

    // Allow a few top BM25-only KIDs to be considered
    private const int Bm25InjectMax = 5;
    private const int Bm25InjectRankCutoff = 10;

    // Lexical overlap analysis window and scaling
    private const int OverlapWindowTopKids = 8;    // analyze top-N fused KIDs
    private const double OverlapGamma = 0.20; // bonus added to fused score: fused2 = fused + γ * overlap_norm
    private const int OverlapNormaliser = 2;    // overlap_norm = min(1, overlap / OverlapNormaliser)

    // Lexical sanity-guard margin:
    // If top fused has 0 overlap and some overlapping candidate is within this margin,
    // prefer the overlapping one.
    private const double LexicalOverrideMargin = 0.15;

    // Minimal English stopword set (generic; safe)
    private static readonly HashSet<string> Stop = new(StringComparer.Ordinal)
    {
        "a","an","the","and","or","but",
        "do","does","did","is","are","was","were","be","been","being",
        "i","you","he","she","it","we","they","me","my","your","our","their",
        "to","of","for","in","on","at","by","with","from","as","about","into","over","under",
        "any","some","this","that","these","those","there","here",
        "can","could","may","might","will","would","shall","should",
        "have","has","had","get","got","how","what","where","when","which","who","whom",
        "please"
    };

    public async Task<SearchResult?> FindAnswerAsync(string question, string language, HashSet<KnowledgeTopic> allowedTopics)
    {
        if (string.IsNullOrWhiteSpace(question)) return null;
        if (string.IsNullOrWhiteSpace(language)) throw new ArgumentException("language must be provided", nameof(language));

        var lang = language.Trim().ToLowerInvariant();
        var qClean = TextPreprocessor.Clean(question); // critical: match exporter

        logger.LogDebug("HybridKB: start | lang={Lang} | raw=\"{Raw}\" | clean=\"{Clean}\"", lang, question, qClean);

        // 1) Semantic shortlist (distinct KIDs; ANN similarity in [0,1])
        var embedding = await embedder.GetEmbeddingAsync(qClean);
        var semanticList = await semanticSearch.SemanticSearchAsync(embedding, lang, allowedTopics, topK: SemanticTopK);
        if (semanticList.Count == 0)
        {
            logger.LogDebug("HybridKB: semantic shortlist is empty.");
            return null;
        }

        logger.LogDebug("HybridKB: semantic KIDs (top 10): {Kids}",
            string.Join(",", semanticList.Take(10).Select(r => r.KnowledgeId)));

        var semanticScoreByKid = new Dictionary<int, double>(semanticList.Count);
        var bestSemanticRowByKid = new Dictionary<int, SearchResult>(semanticList.Count);
        foreach (var row in semanticList)
        {
            if (!semanticScoreByKid.ContainsKey(row.KnowledgeId))
            {
                semanticScoreByKid[row.KnowledgeId] = NormalizeToUnitInterval(row.Score);
                bestSemanticRowByKid[row.KnowledgeId] = row;
            }
        }
        var semanticKidSet = new HashSet<int>(semanticScoreByKid.Keys);

        // 2) BM25 on CLEANED query; expect (QID, Score) with higher=better
        var bm25 = await Task.Run(() => bm25Index.Query(lang, qClean, Bm25TopK));

        // Map QID→KID via exported mapping
        var mappingStore = indexCache.GetKnowledgeBaseMappingStore(lang);
        var qidToKid = mappingStore.QuestionAnswerMapping;

        var bm25RawScoreByKid = new Dictionary<int, double>(Math.Min(bm25.Count, 128));
        var bm25RankByKid = new Dictionary<int, int>(Math.Min(bm25.Count, 128));
        var kidToAnyQid = new Dictionary<int, int>();
        var bm25OnlyToInject = new List<(int Kid, int Rank)>(Bm25InjectMax);

        int rank = 1;
        double maxBm25Score = 0.0;

        foreach (var (qid, rawScore) in bm25)
        {
            if (!qidToKid.TryGetValue(qid, out var kid)) { rank++; continue; }

            // ACL via cache
            if (!knowledgeItemCache.TryGetKnowledgeItemByQuestionId(qid, lang, out var item) || item is null)
            { rank++; continue; }
            if (allowedTopics is not null && allowedTopics.Count > 0 && !allowedTopics.Contains(item.Topic))
            { rank++; continue; }

            if (!bm25RawScoreByKid.TryGetValue(kid, out var prev) || rawScore > prev)
                bm25RawScoreByKid[kid] = rawScore;

            bm25RankByKid.TryAdd(kid, rank);
            kidToAnyQid.TryAdd(kid, qid);

            if (!semanticKidSet.Contains(kid) && bm25OnlyToInject.Count < Bm25InjectMax && rank <= Bm25InjectRankCutoff)
                bm25OnlyToInject.Add((kid, rank));

            if (rawScore > maxBm25Score) maxBm25Score = rawScore;
            rank++;
        }

        logger.LogDebug("HybridKB: BM25 top-KIDs (up to 10): {Kids}", string.Join(",", bm25RankByKid.OrderBy(kv => kv.Value).Take(10).Select(kv => $"{kv.Key}#r{kv.Value}")));

        // 3) Inject a few BM25-only KIDs so fusion can consider them at all
        bool injected = false;
        if (bm25OnlyToInject.Count > 0)
        {
            foreach (var (kid, _) in bm25OnlyToInject)
            {
                if (semanticKidSet.Contains(kid) || !kidToAnyQid.TryGetValue(kid, out var anyQid) || !knowledgeItemCache.TryGetKnowledgeItemByQuestionId(anyQid, lang, out var item) || item is null) 
                    continue;

                bestSemanticRowByKid[kid] = new SearchResult
                {
                    QuestionId = anyQid,
                    KnowledgeId = item.Id,
                    Topic = item.Topic,
                    Answer = item.Answer,
                    Language = item.Language,
                    Score = 0.0 // no ANN sim observed; fusion decides
                };
                    
                semanticScoreByKid[kid] = 0.0;
                semanticKidSet.Add(kid);
                injected = true;
            }
            
            if (injected)
                logger.LogDebug("HybridKB: injected BM25-only KIDs: {Kids}", string.Join(",", bm25OnlyToInject.Select(x => $"{x.Kid}#r{x.Rank}")));
        }

        // 4) Fusion at KID level (prefer linear when BM25 scores exist)
        var fused = new Dictionary<int, double>(semanticKidSet.Count + bm25RawScoreByKid.Count);

        if (maxBm25Score > 0.0)
        {
            var bm25NormByKid = new Dictionary<int, double>(bm25RawScoreByKid.Count);
            foreach (var (kid, raw) in bm25RawScoreByKid)
            {
                var norm = (raw <= 0 || maxBm25Score <= 0) ? 0.0 : (raw / maxBm25Score); // [0,1]
                bm25NormByKid[kid] = NormalizeToUnitInterval(norm);
            }

            foreach (var kid in semanticKidSet.Union(bm25NormByKid.Keys))
            {
                var sem = NormalizeToUnitInterval(semanticScoreByKid.GetValueOrDefault(kid, 0.0));
                var lex = bm25NormByKid.GetValueOrDefault(kid, 0.0);
                fused[kid] = (AlphaSemantic * sem) + (BetaBm25 * lex);
            }
        }
        else
        {
            // Fallback: rank-based RRF
            var semanticRankByKid = semanticScoreByKid
                .OrderByDescending(kv => kv.Value)
                .Select((kv, idx) => (Kid: kv.Key, Rank: idx + 1))
                .ToDictionary(x => x.Kid, x => x.Rank);

            ApplyRrf(semanticRankByKid, fused, RrfSemanticWeight);
            ApplyRrf(bm25RankByKid, fused, RrfLexicalWeight);
        }

        // 5) Add a small, generic bonus for lexical content overlap on the top fused KIDs.
        var queryTokens = TokenizeContent(qClean);
        var finalScore = new Dictionary<int, double>(fused);

        var topKidsForOverlap = fused
            .OrderByDescending(kv => kv.Value)
            .Select(kv => kv.Key)
            .Take(OverlapWindowTopKids)
            .ToList();

        var overlapByKid = new Dictionary<int, int>(topKidsForOverlap.Count);

        foreach (var kid in topKidsForOverlap)
        {
            if (!TryGetAnyQidForKid(kid, kidToAnyQid, qidToKid, out var anyQid)) 
                continue;
            if (!knowledgeItemCache.TryGetKnowledgeItemByQuestionId(anyQid, lang, out var item) || item is null) 
                continue;

            var candTokens = BuildCandidateTokenSet(item); // from item.Questions (preferred) or item.Answer
            int overlap = CountOverlap(queryTokens, candTokens);
            overlapByKid[kid] = overlap;

            double bonus = OverlapGamma * Math.Min(1.0, overlap / (double)OverlapNormaliser);
            finalScore[kid] = fused[kid] + bonus;

            logger.LogDebug("HybridKB: overlap kid={Kid} overlap={Overlap} base={Base:F3} bonus={Bonus:F3} final={Final:F3}", kid, overlap, fused[kid], bonus, finalScore[kid]);
        }

        // FINAL SELECTION with lexical sanity-guard
        var ordered = finalScore
            .OrderByDescending(kv => kv.Value)
            .ThenByDescending(kv => semanticScoreByKid.GetValueOrDefault(kv.Key, 0.0))
            .Select(kv => kv.Key)
            .ToList();

        logger.LogDebug("HybridKB: fused+overlap top-KIDs (up to 10): {Kids}", string.Join(",", ordered.Take(10)));

        // If the top fused has zero overlap, prefer the best overlapping candidate within a small margin.
        int winnerKid = ordered[0];
        int topOverlap = overlapByKid.GetValueOrDefault(winnerKid, 0);

        if (topOverlap == 0)
        {
            double topScore = finalScore[winnerKid];
            int bestOverlapKid = -1;
            double bestOverlapScore = double.NegativeInfinity;

            foreach (var kid in ordered)
            {
                int ov = overlapByKid.GetValueOrDefault(kid, 0);
                if (ov <= 0) 
                    continue;

                double sc = finalScore[kid];
                
                if (sc > bestOverlapScore)
                {
                    bestOverlapScore = sc; 
                    bestOverlapKid = kid;
                }
            }

            if (bestOverlapKid >= 0 && (topScore - bestOverlapScore) <= LexicalOverrideMargin)
            {
                logger.LogDebug("HybridKB: lexical override -> prefer kid={Kid} (overlap>0) over kid={TopKid} (overlap=0) within margin={Margin:F3}.", bestOverlapKid, winnerKid, LexicalOverrideMargin);
                winnerKid = bestOverlapKid;
            }
        }

        // Ensure we have a returnable row; materialize if needed.
        if (!bestSemanticRowByKid.TryGetValue(winnerKid, out var winnerRow))
        {
            var ok = TryMaterializeRowForKid(
                winnerKid, lang,
                knowledgeItemCache,
                qidToKid,
                kidToAnyQid,
                semanticScoreByKid,
                bestSemanticRowByKid,
                logger);

            if (ok) winnerRow = bestSemanticRowByKid[winnerKid];
        }

        if (winnerRow is not null)
        {
            logger.LogDebug("HybridKB: winner Kid={Kid}, Qid={Qid}, SemScore={Sem:F3}", winnerKid, winnerRow.QuestionId, semanticScoreByKid.GetValueOrDefault(winnerKid, 0.0));
            return winnerRow;
        }

        logger.LogDebug("HybridKB: fallback to top semantic.");
        return semanticList[0];
    }

    // ---------------- helpers ----------------

    /// <summary> Normalizes a score to [0,1]: NaN/negatives→0, >1→1, otherwise unchanged. </summary>
    private static double NormalizeToUnitInterval(double v)
    {
        if (double.IsNaN(v) || v < 0) 
            return 0;
        if (v > 1) 
            return 1;
        return v;
    }

    /// <summary> Adds weighted RRF contributions to <paramref name="scores"/>. </summary>
    private static void ApplyRrf(IReadOnlyDictionary<int, int> rankByKid, IDictionary<int, double> scores, double weight)
    {
        if (rankByKid.Count == 0 || weight <= 0) 
            return;

        foreach (var (kid, rank) in rankByKid)
        {
            var contrib = weight * (1.0 / (RrfK + rank));

            scores[kid] = scores.TryGetValue(kid, out var current) 
                ? current + contrib 
                : contrib;
        }
    }

    /// <summary>
    /// Build a returnable SearchResult row for a BM25-only KID so the hybrid can return it.
    /// Tries a QID observed in BM25 first; falls back to any QID from the ANN mapping; respects ACL via cache.
    /// </summary>
    private static bool TryMaterializeRowForKid(
        int kid,
        string lang,
        IKnowledgeItemCacheService knowledgeItemCache,
        IReadOnlyDictionary<int, int> qidToKid,
        IReadOnlyDictionary<int, int> kidToAnyQid,
        IReadOnlyDictionary<int, double> semanticScoreByKid,
        IDictionary<int, SearchResult> bestSemanticRowByKid,
        ILogger logger)
    {
        if (kidToAnyQid.TryGetValue(kid, out var qidFromBm25))
        {
            if (knowledgeItemCache.TryGetKnowledgeItemByQuestionId(qidFromBm25, lang, out var item) && item is not null)
            {
                bestSemanticRowByKid[kid] = new SearchResult
                {
                    QuestionId = qidFromBm25,
                    KnowledgeId = item.Id,
                    Topic = item.Topic,
                    Answer = item.Answer,
                    Language = item.Language,
                    Score = semanticScoreByKid.GetValueOrDefault(kid, 0.0)
                };
                
                logger.LogDebug("HybridKB: materialized row for Kid={Kid} using BM25 Qid={Qid}.", kid, qidFromBm25);

                return true;
            }
        }

        int anyQidFromMap = -1;
        foreach (var kv in qidToKid)
        {
            if (kv.Value != kid)
                continue;
            
            anyQidFromMap = kv.Key; 
            break;
        }

        if (anyQidFromMap >= 0 && knowledgeItemCache.TryGetKnowledgeItemByQuestionId(anyQidFromMap, lang, out var item2) && item2 is not null)
        {
            bestSemanticRowByKid[kid] = new SearchResult
            {
                QuestionId = anyQidFromMap,
                KnowledgeId = item2.Id,
                Topic = item2.Topic,
                Answer = item2.Answer,
                Language = item2.Language,
                Score = semanticScoreByKid.GetValueOrDefault(kid, 0.0)
            };

            logger.LogDebug("HybridKB: materialized row for Kid={Kid} using mapping Qid={Qid}.", kid, anyQidFromMap);

            return true;
        }

        logger.LogDebug("HybridKB: failed to materialize row for Kid={Kid}.", kid);

        return false;
    }

    /// <summary> Gets any QID for the given KID, preferring one from BM25; otherwise from mapping. </summary>
    private static bool TryGetAnyQidForKid(int kid, IReadOnlyDictionary<int, int> kidToAnyQid, IReadOnlyDictionary<int, int> qidToKid, out int qid)
    {
        if (kidToAnyQid.TryGetValue(kid, out qid)) 
            return true;

        foreach (var kv in qidToKid)
        {
            if (kv.Value != kid) 
                continue;
            
            qid = kv.Key; 
            return true;
        }
        
        qid = -1; 

        return false;
    }

    /// <summary>
    /// Tokenizes a string into a set of informative content tokens:
    /// - lowercase; keep letters/digits; remove punctuation
    /// - remove stopwords; require token length ≥ 3
    /// </summary>
    private static HashSet<string> TokenizeContent(string s)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(s)) 
            return set;

        // cheap normalization: replace non-alnum with space, then split
        Span<char> buf = stackalloc char[s.Length];
        int w = 0;
        foreach (var ch in s.ToLowerInvariant())
            buf[w++] = char.IsLetterOrDigit(ch) ? ch : ' ';

        var cleaned = new string(buf[..w]);
        foreach (var raw in cleaned.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw.Length < 3) 
                continue;
            if (Stop.Contains(raw)) 
                continue;
            set.Add(raw);
        }

        return set;
    }

    /// <summary> Builds a candidate token set from KnowledgeItem questions; falls back to answer text. </summary>
    private static HashSet<string> BuildCandidateTokenSet(ShopAssistant.Contracts.Models.KnowledgeBase.KnowledgeItem item)
    {
        if (item.Questions is not { Count: > 0 }) 
            return TokenizeContent(item.Answer);
        
        var acc = new HashSet<string>(StringComparer.Ordinal);
        foreach (var q in item.Questions)
        {
            if (string.IsNullOrWhiteSpace(q)) continue;
            var t = TokenizeContent(q);
            acc.UnionWith(t);
        }

        return acc.Count > 0 
            ? acc 
            : TokenizeContent(item.Answer);
    }

    /// <summary> Counts set intersection. </summary>
    private static int CountOverlap(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) 
            return 0;
        
        int c = 0;
        
        foreach (var t in a) 
            if (b.Contains(t)) 
                c++;

        return c;
    }
}
