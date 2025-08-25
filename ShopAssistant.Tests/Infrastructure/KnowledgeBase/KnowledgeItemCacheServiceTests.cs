using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;
using ShopAssistant.Contracts.Enums;
using ShopAssistant.Contracts.Models.KnowledgeBase;
using ShopAssistant.Infrastructure.KnowledgeBase;

namespace ShopAssistant.Tests.Infrastructure.KnowledgeBase;

[TestFixture]
public class KnowledgeItemCacheServiceTests
{
    private static KnowledgeItem Ki(int id, KnowledgeTopic topic = KnowledgeTopic.Shipping, string lang = "en") => new() { Id = id, Topic = topic, Language = lang, Answer = "A", Questions = ["q"] };

    [Test]
    public void Save_And_TryGet_ByQuestionId_Works()
    {
        var mem = new MemoryCache(new MemoryCacheOptions());
        var cache = new KnowledgeItemCacheService(mem);
        var item = Ki(42);

        cache.SaveKnowledgeItemByQuestionId(1001, "EN", item);

        var ok = cache.TryGetKnowledgeItemByQuestionId(1001, "en", out var got);
        Assert.That(ok, Is.True);
        Assert.That(got, Is.Not.Null);
        Assert.That(got!.Id, Is.EqualTo(42));
    }

    [Test]
    public void Save_And_Find_ByQuestionText_Works_With_Normalization()
    {
        var mem = new MemoryCache(new MemoryCacheOptions());
        var cache = new KnowledgeItemCacheService(mem);
        var item = Ki(55);

        cache.SaveKnowledgeItemByQuestionText("  Where is my order? ", "EN", item);
        var got = cache.FindCachedAnswer("where is my order?", "en", null);

        Assert.That(got, Is.Not.Null);
        Assert.That(got!.Id, Is.EqualTo(55));
    }

    [Test]
    public void TryGet_ByQuestionId_ReturnsFalse_When_Missing()
    {
        var mem = new MemoryCache(new MemoryCacheOptions());
        var cache = new KnowledgeItemCacheService(mem);
        var ok = cache.TryGetKnowledgeItemByQuestionId(999, "en", out var _);
        Assert.That(ok, Is.False);
    }
}
