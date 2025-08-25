using ShopAssistant.Contracts.Enums;
using ShopAssistant.Contracts.Models.Chat;

namespace ShopAssistant.Contracts.Interfaces.KnowledgeBase;

/// <summary>
/// Provides semantic search and question answering from the knowledge base, with topic filtering.
/// </summary>
public interface IKnowledgeBaseQueryService
{
    /// <summary>
    /// Finds the best answer to a question for a given language and list of allowed topics.
    /// </summary>
    /// <param name="question">User's question text.</param>
    /// <param name="language">Language code (e.g., "en", "no").</param>
    /// <param name="allowedTopics">Set of allowed knowledge topics for the current user.</param>
    /// <returns>
    /// The SearchResult object if found and allowed; otherwise null.
    /// </returns>
    Task<SearchResult?> FindAnswerAsync(string question, string language, HashSet<KnowledgeTopic> allowedTopics);
}