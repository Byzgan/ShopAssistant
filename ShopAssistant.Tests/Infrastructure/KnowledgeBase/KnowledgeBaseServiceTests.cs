using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using ShopAssistant.Contracts.Enums;
using ShopAssistant.Contracts.Interfaces.KnowledgeBase;
using ShopAssistant.Contracts.Models.KnowledgeBase;
using ShopAssistant.Infrastructure.KnowledgeBase;

namespace ShopAssistant.Tests.Infrastructure.KnowledgeBase;

/// <summary>
/// Unit tests for KnowledgeBaseService (FAQ and semantic fallback).
/// </summary>
[TestFixture]
public class KnowledgeBaseServiceTests
{
    private Mock<IKnowledgeItemCacheService> _cacheMock = null!;
    private Mock<IKnowledgeBaseQueryService> _queryMock = null!;
    private KnowledgeBaseService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _cacheMock = new Mock<IKnowledgeItemCacheService>();
        _queryMock = new Mock<IKnowledgeBaseQueryService>();
        _service = new KnowledgeBaseService(_cacheMock.Object, _queryMock.Object);
    }
    [Test]
    public async Task FindCachedAnswerAsync_ReturnsKnowledgeItem_WhenItemExistsInCache()
    {
        // Arrange
        var question = "What is your return policy?";
        var language = "en";
        var allowedTopics = new HashSet<KnowledgeTopic> { KnowledgeTopic.Returns };
        var expectedItem = new KnowledgeItem
        {
            Id = 1,
            Topic = KnowledgeTopic.Returns,
            Language = language,
            Answer = "You can return items within 30 days.",
            Questions = [question]
        };

        _cacheMock.Setup(c => c.FindCachedAnswer(question, language, allowedTopics)).Returns(expectedItem);

        // Act
        var result = await _service.FindCachedAnswerAsync(question, language, allowedTopics);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(expectedItem.Id));
        Assert.That(result.Answer, Is.EqualTo(expectedItem.Answer));
        Assert.That(result.Topic, Is.EqualTo(expectedItem.Topic));
        Assert.That(result.Language, Is.EqualTo(expectedItem.Language));
        Assert.That(result.Questions, Is.EquivalentTo(expectedItem.Questions));
    }
    
}
