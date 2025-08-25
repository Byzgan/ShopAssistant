namespace ShopAssistant.Tests.IntentProcessing.IntentDetectors;

using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Contracts.Enums;
using Contracts.Interfaces.Intent;
using Contracts.Interfaces.Localization;
using Contracts.Models.Intent;
using ShopAssistant.IntentProcessing.IntentDetectors;
using Helpers;

[TestFixture]
public class HybridIntentDetectorTests
{
    [Test]
    public async Task MultipleStrongMatches_Consults_Patterns_And_Returns_A_Result()
    {
        // Force pattern-based path (no embeddings)
        var embeddingsCache = new Mock<IIntentDetectorEmbeddingsCacheService>(MockBehavior.Strict);
        embeddingsCache.Setup(e => e.TryGet(out It.Ref<Dictionary<Intent, Dictionary<string, List<float[]>>>>.IsAny!))
            .Returns(false);

        var patternCache = new Mock<IIntentPatternCacheService>(MockBehavior.Strict);
        var patterns = new List<IntentPattern>
        {
            new() { Intent = Intent.Recommend, RequiredTokens = [ new List<string> { "recommend", "suggest" } ] },
            new() { Intent = Intent.ProductSearch, RequiredTokens = [ new List<string> { "find", "search" }, new List<string> { "phone" } ] }
        };
        patternCache.Setup(p => p.GetPatternsForLanguage("en")).Returns(patterns);

        var loc = new Mock<ILocalizationService>(MockBehavior.Loose);
        loc.Setup(l => l.GetMessage(It.IsAny<string>(), "en", It.IsAny<string>()))
           .Returns<string, string, string>((k, _, __) => k);
        loc.Setup(l => l.InitializeCacheAsync()).Returns(Task.CompletedTask);

        var matcher = new IntentPatternMatcher();
        var sut = new HybridIntentDetector(new FakeEmbedder(), embeddingsCache.Object, patternCache.Object, loc.Object, matcher);

        var result = await sut.DetectIntentAsync("en", "please recommend phone");

        // Assert: pipeline executed and we got a result
        patternCache.Verify(p => p.GetPatternsForLanguage("en"), Times.Once);
        Assert.That(result, Is.Not.Null);
    }
}
