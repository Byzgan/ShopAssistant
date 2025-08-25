namespace ShopAssistant.Tests.Infrastructure.TextProcessing.Lexical;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Contracts.Enums;
using ShopAssistant.Contracts.Interfaces.KnowledgeBase;
using ShopAssistant.Contracts.Interfaces.TextProcessing;
using ShopAssistant.Infrastructure.KnowledgeBase;
using ShopAssistant.Infrastructure.TextProcessing.Lexical;
using ShopAssistant.Infrastructure.TextProcessing.SemanticSearch.Embeddings;
using Helpers;

[TestFixture]
public class HybridKnowledgeBaseBm25RankTests
{
    private IBm25QuestionIndex _bm25 = null!;
    private Mock<ISemanticSearchService> _semantic = null!;
    private Mock<ITextEmbedder> _embedder = null!;
    private Mock<IKnowledgeItemCacheService> _cache = null!;
    private EmbeddingIndexCacheService _indexCache = null!;

    [SetUp]
    public void SetUp()
    {
        _bm25 = new Bm25QuestionIndex(new MemoryCache(new MemoryCacheOptions()));
        _semantic = new Mock<ISemanticSearchService>(MockBehavior.Strict);
        _embedder = new Mock<ITextEmbedder>(MockBehavior.Strict);
        _cache = new Mock<IKnowledgeItemCacheService>(MockBehavior.Strict);

        // Provide mapping so QueryService can resolve QID→KID
        var (cfg, _) = TestConfig.CreateWithTempEmbeddings(new Dictionary<int, int> { { 1, 42 } });
        _indexCache = new EmbeddingIndexCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            cfg,
            NullLogger<EmbeddingIndexCacheService>.Instance);
    }

    [Test]
    public async Task Bm25_and_Semantic_Compose_Without_Logger_Errors()
    {
        // semantic shortlist empty → overall null
        _embedder.Setup(e => e.GetEmbeddingAsync(It.IsAny<string>())).ReturnsAsync([0.1f]);
        _semantic.Setup(s => s.SemanticSearchAsync(It.IsAny<float[]>(), "en", It.IsAny<HashSet<KnowledgeTopic>>(), It.IsAny<int>()))
                 .ReturnsAsync([]);

        var service = new HybridKnowledgeBaseQueryService(
            _semantic.Object,
            _embedder.Object,
            _bm25,
            _cache.Object,
            _indexCache,
            NullLogger<HybridKnowledgeBaseQueryService>.Instance);

        var result = await service.FindAnswerAsync("bm25 smoke", "en", []);
        Assert.That(result, Is.Null);
    }
}
