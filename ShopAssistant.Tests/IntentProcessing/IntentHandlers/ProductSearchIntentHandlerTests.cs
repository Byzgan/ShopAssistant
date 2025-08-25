namespace ShopAssistant.Tests.IntentProcessing.IntentHandlers;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;
using Contracts.Interfaces.Integrations;
using Contracts.Interfaces.Localization;
using Contracts.Models.Integrations;
using ShopAssistant.IntentProcessing.IntentHandlers;

[TestFixture]
public class ProductSearchIntentHandlerTests
{
    private static IConfiguration InMemoryConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Languages:Default"] = "en"
        }).Build();

    private static Mock<ILocalizationService> LocalizationLoose()
    {
        var loc = new Mock<ILocalizationService>(MockBehavior.Loose);
        loc.Setup(l => l.GetMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
           .Returns<string, string, string>((key, _, _) => key);
        loc.Setup(l => l.InitializeCacheAsync()).Returns(Task.CompletedTask);
        return loc;
    }

    [Test]
    public async Task Extracts_Brand_From_ProductType_And_Calls_Service()
    {
        var svc = new Mock<IProductSearchService>(MockBehavior.Strict);
        const string stubName = "Stub Product";
        svc.Setup(s => s.SearchProductsAsync(It.IsAny<ProductSearchRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<ProductSearchResult>
           {
               new() { Name = stubName, Price = "99", Url = "https://test/p/1" }
           });

        var loc = LocalizationLoose();
        var sut = new ProductSearchIntentHandler(svc.Object, loc.Object, InMemoryConfig());

        var data = new Dictionary<string, string>
        {
            ["ProductType"] = "smartphone iphone",
            ["Brand"] = "none"
        };

        var res = await sut.HandleAsync(data, "en");
        Assert.That(res, Is.Not.Null);
        Assert.That(res!.Answer, Does.Contain("ResultsHeader"));
        Assert.That(res.Answer, Does.Contain(stubName));
        svc.Verify(s => s.SearchProductsAsync(It.IsAny<ProductSearchRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task NoMatches_Returns_Localized_Message()
    {
        var svc = new Mock<IProductSearchService>(MockBehavior.Strict);
        svc.Setup(s => s.SearchProductsAsync(It.IsAny<ProductSearchRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Enumerable.Empty<ProductSearchResult>().ToList());

        var loc = LocalizationLoose();
        var sut = new ProductSearchIntentHandler(svc.Object, loc.Object, InMemoryConfig());

        var data = new Dictionary<string, string>
        {
            ["ProductType"] = "smartphone",
            ["Brand"] = "none"
        };

        var res = await sut.HandleAsync(data, "en");
        Assert.That(res, Is.Not.Null);
        Assert.That(res!.Answer, Does.Contain("NoResults"));
        svc.Verify(s => s.SearchProductsAsync(It.IsAny<ProductSearchRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
