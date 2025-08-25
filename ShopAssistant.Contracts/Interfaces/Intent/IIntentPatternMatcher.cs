namespace ShopAssistant.Contracts.Interfaces.Intent;

using TextProcessing;
using ShopAssistant.Contracts.Models.Intent;


/// <summary>
/// Interface for robust matching between user input or KB questions and intent patterns.
/// </summary>
public interface IIntentPatternMatcher
{
    /// <summary>
    /// Matches the given message to an intent pattern using negative filtering, regex, partial keyword, and semantic matching.
    /// </summary>
    /// <param name="language">Language code ("en", "no", etc.).</param>
    /// <param name="message">User message or KB question.</param>
    /// <param name="pattern">Intent pattern to match against.</param>
    /// <param name="stemmer">Stemmer instance for the given language.</param>
    /// <param name="inputEmbedding">Precomputed embedding for the input (optional, used for semantic match).</param>
    /// <param name="patternEmbeddings">Precomputed embeddings for pattern phrases (optional, used for semantic match).</param>
    /// <returns>Pattern match result with match status, type, score, and matched phrase if any.</returns>
    IntentPatternMatchResult Match(
        string language,
        string message,
        IntentPattern pattern,
        IStemmer stemmer,
        float[]? inputEmbedding = null,
        List<float[]>? patternEmbeddings = null);
}