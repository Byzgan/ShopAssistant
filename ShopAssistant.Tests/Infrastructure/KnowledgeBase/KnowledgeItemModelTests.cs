using NUnit.Framework;
using ShopAssistant.Contracts.Enums;
using ShopAssistant.Contracts.Models.KnowledgeBase;
using System.Collections.Generic;

namespace ShopAssistant.Tests.Infrastructure.KnowledgeBase;

[TestFixture]
public class KnowledgeItemModelTests
{
    [Test]
    public void KnowledgeItem_Required_Members_Are_Set()
    {
        var item = new KnowledgeItem
        {
            Id = 1,
            Topic = KnowledgeTopic.Shipping,
            Language = "en",
            Answer = "Use the tracking link.",
            Questions = ["Where is my order?"]
        };

        Assert.That(item.Language, Is.EqualTo("en"));
        Assert.That(item.Answer, Is.Not.Empty);
        Assert.That(item.Questions, Is.Not.Null);
        Assert.That(item.Questions.Count, Is.GreaterThan(0));
    }
}