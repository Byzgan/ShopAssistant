namespace ShopAssistant.Infrastructure.KnowledgeBase;

using System.Threading.Tasks;
using Contracts.Enums;
using ShopAssistant.Contracts.Interfaces.KnowledgeBase;
using ShopAssistant.Contracts.Models.Chat;
using ShopAssistant.Contracts.Models.KnowledgeBase;

/// <summary>
/// Implements all knowledge base operations: FAQ cache retrieval and update, semantic search fallback.
/// </summary>
public class KnowledgeBaseService(IKnowledgeItemCacheService knowledgeItemCache, IKnowledgeBaseQueryService kbQueryService) : IKnowledgeBaseService
{
    /// <inheritdoc/>
    public async Task<KnowledgeItem?> FindCachedAnswerAsync(string question, string language, HashSet<KnowledgeTopic>? allowedTopics)
    {
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(language))
            return null;

        var result = await Task.Run(() => knowledgeItemCache.FindCachedAnswer(question, language, allowedTopics));

        return result;
    }

    /// <inheritdoc/>
    public Task SaveAnswerToCacheAsync(string question, string language, KnowledgeItem item)
    {
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(language))
            return Task.CompletedTask;

        knowledgeItemCache.SaveKnowledgeItemByQuestionText(question, language, item);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<SearchResult?> FindSemanticAnswerAsync(string question, string language, HashSet<KnowledgeTopic> allowedTopics)
    {
        return kbQueryService.FindAnswerAsync(question, language, allowedTopics);
    }
}