namespace ShopAssistant.Infrastructure.KnowledgeBase;

using Microsoft.Extensions.Caching.Memory;
using Contracts.Enums;
using ShopAssistant.Contracts.Interfaces.KnowledgeBase;
using ShopAssistant.Contracts.Models.KnowledgeBase;

public class KnowledgeItemCacheService(IMemoryCache memoryCache) : IKnowledgeItemCacheService
{
    private const string KnowledgeItemsByQuestionIdCacheKey = "KnowledgeItemByQuestionId";
    private const string KnowledgeItemsByQuestionTextCacheKey = "KnowledgeItemByQuestionText";

    private static object QuestionIdKey(int id, string lang) => (KnowledgeItemsByQuestionIdCacheKey, id, lang.Trim().ToLowerInvariant());

    private static object QuestionTextKey(string q, string lang) => (KnowledgeItemsByQuestionTextCacheKey, q.Trim().ToLowerInvariant(), lang.Trim().ToLowerInvariant());

    public KnowledgeItem? GetKnowledgeItemByQuestionId(int questionId, string language)
    {
        if (memoryCache.TryGetValue(QuestionIdKey(questionId, language), out var obj) && obj is KnowledgeItem item)
            return item;

        return null;
    }

    public bool TryGetKnowledgeItemByQuestionId(int questionId, string language, out KnowledgeItem? knowledgeItem)
    {
        if (memoryCache.TryGetValue(QuestionIdKey(questionId, language), out var obj) && obj is KnowledgeItem item)
        {
            knowledgeItem = item;
            return true;
        }

        knowledgeItem = null;
        return false;
    }

    public KnowledgeItem? FindCachedAnswer(string question, string language, HashSet<KnowledgeTopic>? allowedTopics)
    {
        var normalizedQuestion = question.Trim().ToLowerInvariant();

        var item = memoryCache.Get<KnowledgeItem>(QuestionTextKey(normalizedQuestion, language));

        if (item is null || allowedTopics is null || !allowedTopics.Contains(item.Topic))
            return null;

        return item;
    }

    public void SaveKnowledgeItemByQuestionId(int questionId, string language, KnowledgeItem knowledgeItem)
    {
        memoryCache.Set(QuestionIdKey(questionId, language), knowledgeItem, new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.High
        });
    }

    public void SaveKnowledgeItemByQuestionText(string question, string language, KnowledgeItem knowledgeItem)
    {
        var normalizedQuestion = question.Trim().ToLowerInvariant();

        memoryCache.Set(QuestionTextKey(normalizedQuestion, language), knowledgeItem, new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.Normal
        });
    }
}
