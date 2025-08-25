namespace ShopAssistant.Contracts.Interfaces.TextProcessing;

/// <summary>
/// High-performance, in-memory BM25 index over *question variants* keyed by integer QuestionId.
/// Aligned with your ANN ids so BM25 and vector search can be fused.
/// </summary>
public interface IBm25QuestionIndex
{
    /// <summary>
    /// Rebuilds the BM25 index for a language with (QuestionId, Text) pairs.
    /// Existing data for that language is replaced atomically.
    /// </summary>
    void Build(string language, IReadOnlyList<(int QuestionId, string Text)> questions);

    /// <summary>
    /// Executes a BM25 query and returns top-K (QuestionId, Score) pairs (larger = better).
    /// No per-query caching is used; tokenization happens inline.
    /// </summary>
    IReadOnlyList<(int QuestionId, double Score)> Query(string language, string queryText, int topK);

    /// <summary>Removes all data for a specific language (used for refresh).</summary>
    void ClearLanguage(string language);
}