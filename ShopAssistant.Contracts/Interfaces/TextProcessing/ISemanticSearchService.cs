namespace ShopAssistant.Contracts.Interfaces.TextProcessing;

using Enums;
using ShopAssistant.Contracts.Models.Chat;

/// <summary>
/// Interface for performing semantic vector search over the knowledge base.
/// </summary>
public interface ISemanticSearchService
{
    /// <summary>
    /// Searches the knowledge base for the most semantically relevant questions and their linked answers.
    /// </summary>
    /// <param name="embedding">
    /// The input vector embedding (representing the user's question or phrase).
    /// </param>
    /// <param name="language">
    /// The ISO language code to filter results by (e.g., "en", "no").
    /// </param>
    /// <param name="allowedTopics">
    /// An optional set of <see cref="KnowledgeTopic"/> values to restrict the search to specific topics.
    /// If null, all topics are considered allowed.
    /// </param>
    /// <param name="topK">
    /// The maximum number of top relevant results to return (default: 1).
    /// </param>
    /// <returns>
    /// A list of <see cref="SearchResult"/> objects representing the most similar knowledge base entries,
    /// ordered by semantic similarity (most relevant first).
    /// </returns>
    Task<List<SearchResult>> SemanticSearchAsync(float[] embedding, string language, HashSet<KnowledgeTopic>? allowedTopics, int topK = 1);
    
    /// <summary>
    /// Searches the knowledge base for the single most semantically relevant question and its linked answer.
    /// </summary>
    /// <param name="embedding">
    /// The input vector embedding (representing the user's question or phrase).
    /// </param>
    /// <param name="language">
    /// The ISO language code to filter results by (e.g., "en", "no").
    /// </param>
    /// <param name="allowedTopics">
    /// An optional set of <see cref="KnowledgeTopic"/> values to restrict the search to specific topics.
    /// If null, all topics are considered allowed.
    /// </param>
    /// <returns>
    /// The <see cref="SearchResult"/> object representing the most similar knowledge base entry,
    /// or null if no match is found.
    /// </returns>
    Task<SearchResult?> GetBestSemanticMatchAsync(float[] embedding, string language, HashSet<KnowledgeTopic>? allowedTopics);
}