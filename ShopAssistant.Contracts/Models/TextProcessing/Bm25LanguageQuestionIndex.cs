namespace ShopAssistant.Contracts.Models.TextProcessing;

using System.Collections.Concurrent;

/// <summary>
/// Per-language BM25 index container for *question variants* (no "documents" in this domain).
/// Keeping this in Contracts lets multiple layers (infra, admin tools) share the same shape.
/// </summary>
public class Bm25LanguageQuestionIndex
{
    /// <summary>
    /// Inverted index: term -> (questionId -> term frequency in that *question variant*).
    /// </summary>
    public required ConcurrentDictionary<string, ConcurrentDictionary<int, int>> Inv { get; init; }

    /// <summary>
    /// Token counts per question variant: questionId -> number of tokens in that question text.
    /// </summary>
    public required ConcurrentDictionary<int, int> QuestionLengths { get; init; }

    /// <summary>
    /// Average token count across all question variants for this language.
    /// </summary>
    public required double AvgQuestionLength { get; init; }

    /// <summary>
    /// Total number of indexed question variants for this language.
    /// </summary>
    public required int QuestionCount { get; init; }

    /// <summary>
    /// Number of distinct lexical terms (vocabulary size) for this language.
    /// </summary>
    public required int VocabularySize { get; init; }
}