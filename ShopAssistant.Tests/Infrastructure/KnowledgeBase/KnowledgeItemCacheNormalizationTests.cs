using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;
using ShopAssistant.Contracts.Enums;
using ShopAssistant.Contracts.Models.KnowledgeBase;
using ShopAssistant.Infrastructure.KnowledgeBase;

namespace ShopAssistant.Tests.Infrastructure.KnowledgeBase;

[TestFixture]
public class KnowledgeItemCacheNormalizationTests
{
    private static KnowledgeItem Ki(int id, KnowledgeTopic topic = KnowledgeTopic.Shipping, string lang = "en") =>
        new KnowledgeItem { Id = id, Topic = topic, Language = lang, Answer = "A", Questions = new List<string> { "q" } };

    [Test]
    public void FindCachedAnswer_Respects_Topic_Filter_And_Normalizes()
    {
        var mem = new MemoryCache(new MemoryCacheOptions());
        var cache = new KnowledgeItemCacheService(mem);
        var item = Ki(77, KnowledgeTopic.Order);

        cache.SaveKnowledgeItemByQuestionText("  Track order  ", "EN", item);

        var allowedWrong = new HashSet<KnowledgeTopic> { KnowledgeTopic.Shipping };
        var missWrongTopic = cache.FindCachedAnswer("Track Order", "en", allowedWrong);
        Assert.That(missWrongTopic, Is.Null);

        var allowedRight = new HashSet<KnowledgeTopic> { KnowledgeTopic.Order };
        var hit = cache.FindCachedAnswer(" track order ", "en", allowedRight);

        Assert.That(hit, Is.Not.Null);
        Assert.That(hit!.Id, Is.EqualTo(77));
    }

    [Test]
    public void TryGet_ReturnsFalse_WhenMissing()
    {
        var mem = new MemoryCache(new MemoryCacheOptions());
        var cache = new KnowledgeItemCacheService(mem);
        var ok = cache.TryGetKnowledgeItemByQuestionId(999, "en", out var _);
        Assert.That(ok, Is.False);
    }
}