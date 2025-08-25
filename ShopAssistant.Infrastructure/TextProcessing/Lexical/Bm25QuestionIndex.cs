namespace ShopAssistant.Infrastructure.TextProcessing.Lexical;

using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using ShopAssistant.Contracts.Interfaces.TextProcessing;
using ShopAssistant.Contracts.Models.TextProcessing;
using Normalization;

/// <summary>
/// In-memory BM25 index optimized for *question variants* (per language).
/// Simple caching strategy:
/// - The entire per-language index is stored both in-process (fast path) and as a single
///   object in IMemoryCache (for eviction/refresh control).
/// - No per-query caching (tokenization happens inline per call).
/// </summary>
public class Bm25QuestionIndex(IMemoryCache cache) : IBm25QuestionIndex
{
    // BM25 parameters tuned for short/medium FAQ-like question texts
    private const double K1 = 1.2;
    private const double B = 0.75;

    // Inverted index for each language:
    // Dictionary structure: term -> (questionId -> term frequency in that question variant)
    // - Outer dictionary key: lexical term (string)
    // - Inner dictionary key: questionId (int), value: term frequency (int)
    // - Top-level key: language (string, normalized)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<int, int>>> _inv = new(StringComparer.OrdinalIgnoreCase);

    // Stores per-language mappings of questionId to token count.
    // Structure: language (string) -> (questionId (int) -> token count (int))
    // Used to compute BM25 document length normalization and scoring.
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, int>> _qLen = new(StringComparer.OrdinalIgnoreCase);

    // Stores the average question token length for each language.
    // Key: language (string, normalized), Value: average token length (double).
    private readonly ConcurrentDictionary<string, double> _avgQLen = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Rebuilds the index for a language using (QuestionId, Text) tuples.
    /// Stores the result in-process and as one IMemoryCache entry.
    /// </summary>
    public void Build(string language, IReadOnlyList<(int QuestionId, string Text)> questions)
    {
        if (string.IsNullOrWhiteSpace(language)) return;
        var lang = language.Trim().ToLowerInvariant();

        var inv = new ConcurrentDictionary<string, ConcurrentDictionary<int, int>>(StringComparer.Ordinal);
        var qLen = new ConcurrentDictionary<int, int>();

        int totalTokens = 0, questionCount = 0;

        foreach (var (qid, text) in questions)
        {
            if (qid <= 0 || string.IsNullOrWhiteSpace(text)) 
                continue;

            var tokens = TextNormalization.Tokenize(lang, text, deduplicate: false);
            qLen[qid] = tokens.Length;
            totalTokens += tokens.Length;
            questionCount++;

            // Per-question term frequency (count real occurrences; do not deduplicate here).
            var tf = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var t in tokens)
            {
                tf[t] = tf.TryGetValue(t, out var c) ? c + 1 : 1;
            }

            foreach (var (term, freq) in tf)
            {
                var postings = inv.GetOrAdd(term, static _ => new ConcurrentDictionary<int, int>());
                postings[qid] = freq;
            }
        }

        var avgQLen = questionCount == 0 ? 0d : (double)totalTokens / questionCount;

        // Publish to fast-path fields
        _inv[lang] = inv;
        _qLen[lang] = qLen;
        _avgQLen[lang] = avgQLen;

        // Persist as a single object into IMemoryCache for managed eviction (required fields set)
        var container = new Bm25LanguageQuestionIndex
        {
            Inv = inv,
            QuestionLengths = qLen,
            AvgQuestionLength = avgQLen,
            QuestionCount = questionCount,
            VocabularySize = inv.Count
        };

        cache.Set(LangIndexKey(lang), container, new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.High
        });
    }

    /// <summary>
    /// Execute a BM25 query. Tokenization is performed inline; no per-query caching is used.
    /// Optimized to avoid extra allocations and to select topK via a min-heap.
    /// </summary>
    public IReadOnlyList<(int QuestionId, double Score)> Query(string language, string queryText, int topK)
    {
        if (topK <= 0 || string.IsNullOrWhiteSpace(language) || string.IsNullOrWhiteSpace(queryText)) 
            return [];

        var lang = language.Trim().ToLowerInvariant();

        // Ensure fast-path fields exist; if not, restore from cache.
        if (!TryEnsureLanguage(lang))
            return [];

        var inv = _inv[lang];
        var qLen = _qLen[lang];
        var avgQLen = _avgQLen[lang];

        // Normalized & de-duplicated tokens (no need for additional Distinct later).
        var terms = TextNormalization.Tokenize(lang, queryText.Trim(), deduplicate: true);
        if (terms.Length == 0 || avgQLen <= 0) 
            return [];

        var n = qLen.Count;
        if (n == 0) 
            return [];

        // Precompute IDF for unique query terms that exist in the vocabulary.
        // We intentionally skip terms missing from the index to avoid extra dictionary lookups later.
        var idf = new Dictionary<string, double>(terms.Length, StringComparer.Ordinal);
        foreach (var t in terms)
        {
            if (idf.ContainsKey(t)) 
                continue;

            if (!inv.TryGetValue(t, out var postings) || postings.Count == 0)
                continue;

            var df = postings.Count;
            var value = Math.Log((n - df + 0.5) / (df + 0.5) + 1d);
            if (value > 0d) 
                idf[t] = value; // zero/negative idf carries no weight
        }

        if (idf.Count == 0) 
            return [];

        // Accumulate BM25 scores: qid -> score
        var scores = new Dictionary<int, double>();
        foreach (var (term, idfT) in idf)
        {
            // We already checked inv.ContainsKey(term) above; TryGetValue should succeed.
            if (!inv.TryGetValue(term, out var postings) || postings.Count == 0) continue;

            foreach (var (qid, tf) in postings)
            {
                if (!qLen.TryGetValue(qid, out var ql) || ql <= 0)
                    continue;

                var denom = tf + K1 * (1d - B + B * (ql / avgQLen));
                var contrib = idfT * (tf * (K1 + 1d)) / denom;

                if (scores.TryGetValue(qid, out var s))
                    scores[qid] = s + contrib;
                else
                    scores[qid] = contrib;
            }
        }

        if (scores.Count == 0) 
            return [];

        // Select topK via a min-heap to avoid sorting the entire scores map (O(M log K) vs O(M log M)).
        var heap = new PriorityQueue<(int Qid, double Score), double>();
        foreach (var (qid, s) in scores)
        {
            if (heap.Count < topK)
            {
                heap.Enqueue((qid, s), s);
            }
            else if (s > heap.Peek().Score)
            {
                heap.Dequeue();
                heap.Enqueue((qid, s), s);
            }
        }

        // Materialize result in descending score order with stable tie-break by QuestionId ascending.
        var resultCount = Math.Min(topK, heap.Count);
        var buffer = new (int QuestionId, double Score)[resultCount];
        for (int i = resultCount - 1; i >= 0; i--)
        {
            var item = heap.Dequeue();
            buffer[i] = (item.Qid, item.Score);
        }

        Array.Sort(buffer, static (a, b) =>
        {
            var cmp = b.Score.CompareTo(a.Score);
            return cmp != 0 ? cmp : a.QuestionId.CompareTo(b.QuestionId);
        });

        return buffer;
    }

    /// <summary>
    /// Removes all data for a specific language (both fields and cache entry).
    /// </summary>
    public void ClearLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language)) 
            return;
        
        var lang = language.Trim().ToLowerInvariant();

        _inv.TryRemove(lang, out _);
        _qLen.TryRemove(lang, out _);
        _avgQLen.TryRemove(lang, out _);

        cache.Remove(LangIndexKey(lang));
    }

    // --------------------------- Internal helpers ---------------------------------------

    /// <summary>
    /// Ensures in-process fields are available; if missing, tries to restore from IMemoryCache.
    /// </summary>
    private bool TryEnsureLanguage(string lang)
    {
        if (_inv.ContainsKey(lang) && _qLen.ContainsKey(lang) && _avgQLen.ContainsKey(lang))
            return true;

        if (!cache.TryGetValue(LangIndexKey(lang), out Bm25LanguageQuestionIndex? stored) || stored is null)
            return false;

        _inv[lang] = stored.Inv;
        _qLen[lang] = stored.QuestionLengths;
        _avgQLen[lang] = stored.AvgQuestionLength;

        return true;
    }

    private static string LangIndexKey(string lang) => $"bm25:lang:{lang}";
}
