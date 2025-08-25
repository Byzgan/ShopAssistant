namespace ShopAssistant.Contracts.Interfaces.KnowledgeBase;

using System.Collections.Generic;
using System.Threading.Tasks;
using Enums;
using ShopAssistant.Contracts.Models.Chat;
using ShopAssistant.Contracts.Models.KnowledgeBase;

/// <summary>
/// Facade interface for all knowledge base operations: FAQ cache, semantic search, and knowledge enrichment.
/// </summary>
public interface IKnowledgeBaseService
{
    /// <summary>
    /// Searches for an answer to the user's question in the FAQ cache with topic-based access control.
    /// </summary>
    /// <param name="question">The user question string.</param>
    /// <param name="language">ISO language code (e.g., "en", "no").</param>
    /// <param name="allowedTopics">Set of topics the user is allowed to query.</param>
    /// <returns>The cached knowledge item if found, or null.</returns>
    Task<KnowledgeItem?> FindCachedAnswerAsync(string question, string language, HashSet<KnowledgeTopic>? allowedTopics);

    /// <summary>
    /// Saves a new knowledge item (FAQ answer) into the FAQ cache for future fast retrieval.
    /// </summary>
    /// <param name="question">The question string as posed by the user.</param>
    /// <param name="language">ISO language code.</param>
    /// <param name="item">The full knowledge item to cache.</param>
    /// <returns>Asynchronous operation.</returns>
    Task SaveAnswerToCacheAsync(string question, string language, KnowledgeItem item);

    /// <summary>
    /// Performs semantic (vector-based) search for an answer, with topic access control.
    /// </summary>
    /// <param name="question">The user question string.</param>
    /// <param name="language">ISO language code.</param>
    /// <param name="allowedTopics">Set of topics the user is allowed to query.</param>
    Task<SearchResult?> FindSemanticAnswerAsync(string question, string language, HashSet<KnowledgeTopic> allowedTopics);
}