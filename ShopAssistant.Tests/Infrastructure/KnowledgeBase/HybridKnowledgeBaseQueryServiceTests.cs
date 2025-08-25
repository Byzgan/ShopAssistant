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
public class HybridKnowledgeBaseQueryServiceTests
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

        // Provide at least one mapping; content rarely matters for "null when shortlist empty"
        var (cfg, _) = TestConfig.CreateWithTempEmbeddings(new Dictionary<int, int> { { 101, 11 } });
        _indexCache = new EmbeddingIndexCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            cfg,
            NullLogger<EmbeddingIndexCacheService>.Instance);
    }

    [Test]
    public async Task ReturnsNull_OnEmptyQuestion()
    {
        var sut = new HybridKnowledgeBaseQueryService(_semantic.Object, _embedder.Object, _bm25, _cache.Object, _indexCache, NullLogger<HybridKnowledgeBaseQueryService>.Instance);

        var result = await sut.FindAnswerAsync("", "en", []);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ReturnsNull_WhenSemanticShortlistIsEmpty()
    {
        _embedder.Setup(e => e.GetEmbeddingAsync(It.IsAny<string>())).ReturnsAsync([0.1f, 0.2f, 0.3f]);
        _semantic.Setup(s => s.SemanticSearchAsync(It.IsAny<float[]>(), "en", It.IsAny<HashSet<KnowledgeTopic>>(), It.IsAny<int>()))
                 .ReturnsAsync([]);

        // BM25 results (if any) will be filtered by cache: return false
        _cache.Setup(c => c.TryGetKnowledgeItemByQuestionId(It.IsAny<int>(), "en",
                out It.Ref<ShopAssistant.Contracts.Models.KnowledgeBase.KnowledgeItem?>.IsAny))
              .Returns(false);

        var sut = new HybridKnowledgeBaseQueryService(_semantic.Object, _embedder.Object, _bm25, _cache.Object, _indexCache, NullLogger<HybridKnowledgeBaseQueryService>.Instance);

        var result = await sut.FindAnswerAsync("test", "en", []);
        Assert.That(result, Is.Null);
    }
}
