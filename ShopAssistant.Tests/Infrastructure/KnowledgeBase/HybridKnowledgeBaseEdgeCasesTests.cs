namespace ShopAssistant.Tests.Infrastructure.KnowledgeBase;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using ShopAssistant.Contracts.Enums;
using ShopAssistant.Contracts.Interfaces.KnowledgeBase;
using ShopAssistant.Contracts.Interfaces.TextProcessing;
using ShopAssistant.Contracts.Models.Chat;
using ShopAssistant.Infrastructure.KnowledgeBase;
using ShopAssistant.Infrastructure.TextProcessing.Lexical;
using ShopAssistant.Infrastructure.TextProcessing.SemanticSearch.Embeddings;
using ShopAssistant.Tests.Helpers;
using System.Collections.Generic;
using System.Threading.Tasks;

[TestFixture]
public class HybridKnowledgeBaseEdgeCasesTests
{
    private Mock<ISemanticSearchService> _semantic = null!;
    private Mock<ITextEmbedder> _embedder = null!;
    private IBm25QuestionIndex _bm25 = null!;
    private Mock<IKnowledgeItemCacheService> _cache = null!;
    private EmbeddingIndexCacheService _indexCache = null!;

    [SetUp]
    public void SetUp()
    {
        _semantic = new Mock<ISemanticSearchService>(MockBehavior.Strict);
        _embedder = new Mock<ITextEmbedder>(MockBehavior.Strict);
        _bm25 = new Bm25QuestionIndex(new MemoryCache(new MemoryCacheOptions()));
        _cache = new Mock<IKnowledgeItemCacheService>(MockBehavior.Strict);

        // Ensure kb_meta_{lang}.json is a top-level array of {QuestionId, KnowledgeId}
        var (cfg, _) = TestConfig.CreateWithTempEmbeddings(new[] { (2001, 10) });
        _indexCache = new EmbeddingIndexCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            cfg,
            NullLogger<EmbeddingIndexCacheService>.Instance);
    }

    private HybridKnowledgeBaseQueryService CreateSut()
        => new(
            _semantic.Object,
            _embedder.Object,
            _bm25,
            _cache.Object,
            _indexCache,
            NullLogger<HybridKnowledgeBaseQueryService>.Instance);

    [Test]
    public async Task EmptyQuestion_ReturnsNull()
    {
        var sut = CreateSut();
        var result = await sut.FindAnswerAsync("", "en", new HashSet<KnowledgeTopic>());
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task NoSemanticResults_ReturnsNull()
    {
        _embedder.Setup(e => e.GetEmbeddingAsync(It.IsAny<string>())).ReturnsAsync(new float[] { 0.1f });
        _semantic.Setup(s => s.SemanticSearchAsync(It.IsAny<float[]>(), "en", It.IsAny<HashSet<KnowledgeTopic>>(), It.IsAny<int>()))
                 .ReturnsAsync(new List<SearchResult>());

        _cache.Setup(c => c.TryGetKnowledgeItemByQuestionId(It.IsAny<int>(), "en",
                out It.Ref<ShopAssistant.Contracts.Models.KnowledgeBase.KnowledgeItem?>.IsAny))
              .Returns(false);

        var sut = CreateSut();
        var result = await sut.FindAnswerAsync("q", "en", new HashSet<KnowledgeTopic>());
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task FallsBack_ToTopSemantic_WhenMaterializationFails()
    {
        var annRow = new SearchResult { KnowledgeId = 10, QuestionId = 2001, Score = 0.92 };

        _embedder.Setup(e => e.GetEmbeddingAsync(It.IsAny<string>())).ReturnsAsync(new float[] { 0.5f, 0.5f });
        _semantic.Setup(s => s.SemanticSearchAsync(It.IsAny<float[]>(), "en", It.IsAny<HashSet<KnowledgeTopic>>(), It.IsAny<int>()))
                 .ReturnsAsync(new List<SearchResult> { annRow });

        // Simulate cache miss so service falls back to best semantic KID
        _cache.Setup(c => c.TryGetKnowledgeItemByQuestionId(It.IsAny<int>(), "en",
                out It.Ref<ShopAssistant.Contracts.Models.KnowledgeBase.KnowledgeItem?>.IsAny))
              .Returns(false);

        var sut = CreateSut();
        var result = await sut.FindAnswerAsync("status", "en", new HashSet<KnowledgeTopic>());

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.KnowledgeId, Is.EqualTo(10));
    }
}
