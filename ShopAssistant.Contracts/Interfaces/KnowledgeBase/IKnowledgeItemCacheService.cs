using ShopAssistant.Contracts.Enums;
using ShopAssistant.Contracts.Models.KnowledgeBase;

namespace ShopAssistant.Contracts.Interfaces.KnowledgeBase;

/// <summary>
/// Service for caching KnowledgeItem.
/// </summary>
public interface IKnowledgeItemCacheService
{
    /// <summary>
    /// Gets the KnowledgeItem for a given question ID and language, if any.
    /// </summary>
    /// <param name="questionId">The question identifier.</param>
    /// <param name="language">Language code (e.g., "en", "no").</param>
    /// <returns>The cached KnowledgeItem or <c>null</c> if not found.</returns>
    KnowledgeItem? GetKnowledgeItemByQuestionId(int questionId, string language);

    /// <summary>
    /// Fast in-memory lookup: map QuestionId -> (KnowledgeId, Topic).
    /// </summary>
    /// <param name="questionId">The question identifier.</param>
    /// <param name="language">Language code (e.g., "en", "no").</param>
    /// <param name="knowledgeItem">Out parameter for the found KnowledgeItem, or <c>null</c> if not found.</param>
    /// <returns>True if found, otherwise false.</returns>
    bool TryGetKnowledgeItemByQuestionId(int questionId, string language, out KnowledgeItem? knowledgeItem);

    /// <summary>
    /// Gets the KnowledgeItem for a question and language, if any.
    /// </summary>
    /// <param name="question">The question text.</param>
    /// <param name="language">Language code (e.g., "en", "no").</param>
    /// <param name="allowedTopics">Set of topics the user is allowed to query.</param>
    /// <returns>The cached KnowledgeItem or <c>null</c> if not found.</returns>
    KnowledgeItem? FindCachedAnswer(string question, string language, HashSet<KnowledgeTopic>? allowedTopics);

    /// <summary>
    /// Saves a KnowledgeItem to the cache using question ID and language as the key.
    /// </summary>
    /// <param name="questionId">The question identifier.</param>
    /// <param name="language">Language code (e.g., "en", "no").</param>
    /// <param name="knowledgeItem">Knowledge item to cache.</param>
    void SaveKnowledgeItemByQuestionId(int questionId, string language, KnowledgeItem knowledgeItem);

    /// <summary>
    /// Saves KnowledgeItem to the cache for a question and language.
    /// </summary>
    /// <param name="question">The question text.</param>
    /// <param name="language">Language code.</param>
    /// <param name="knowledgeItem">Knowledge item to cache.</param>
    void SaveKnowledgeItemByQuestionText(string question, string language, KnowledgeItem knowledgeItem);
}