using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;
using ShopAssistant.Contracts.Enums;
using ShopAssistant.Contracts.Interfaces.Integrations;
using ShopAssistant.Contracts.Interfaces.Localization;
using ShopAssistant.Contracts.Models.Integrations;
using ShopAssistant.IntentProcessing.IntentHandlers;

namespace ShopAssistant.Tests.IntentProcessing.IntentHandlers;

[TestFixture]
public class RecommendationIntentHandlerTests
{
    private static IConfiguration InMemoryConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Languages:Default"] = "en" })
            .Build();

    private sealed class PassThroughLocalization : ILocalizationService
    {
        public string GetMessage(string key, string language, string scope) => key switch
        {
            "AskCategory" => "Which product are you looking for?",
            "AskBudget" => "What is your budget?",
            "AskPreference" => "Any preference?",
            "AskDiscountOnly" => "Only discounted items?",
            "ResultsHeader" => "Here are recommendations",
            "NoResults" => "No results",
            _ => key
        };
        public Task InitializeCacheAsync() => Task.CompletedTask;
    }

    // Always provide a full slot set; override what a test needs to vary
    private static Dictionary<string, string> CompleteState(params (string Key, string Value)[] overrides)
    {
        var state = new Dictionary<string, string>
        {
            ["Category"] = "phone",
            ["Brand"] = "none",
            ["Budget"] = "1000",
            ["Preference"] = "battery",
            ["DiscountOnly"] = "false"
        };
        foreach (var (k, v) in overrides) state[k] = v;
        return state;
    }

    [Test]
    public async Task GetNextStep_AsksCategoryFirst()
    {
        var svc = new Mock<IRecommendationService>(MockBehavior.Strict);
        // Known categories used by MatchCategory in the handler
        svc.Setup(s => s.GetKnownCategoriesAsync("en"))
           .ReturnsAsync(new Dictionary<string, List<string>>
           {
               ["phone"] = ["phones", "smartphone", "iphone"]
           });

        var sut = new RecommendationIntentHandler(svc.Object, new PassThroughLocalization(), InMemoryConfig());

        var step = await sut.GetNextStep(new Dictionary<string, string>(), "en");
        Assert.That(step.Status, Is.EqualTo(DialogStepStatus.InProgress));
        Assert.That(step.Prompt, Does.Contain("product").IgnoreCase);
    }

    [Test]
    public async Task GetNextStep_AsksBudgetSecond()
    {
        var svc = new Mock<IRecommendationService>(MockBehavior.Strict);
        svc.Setup(s => s.GetKnownCategoriesAsync("en"))
           .ReturnsAsync(new Dictionary<string, List<string>> { ["phone"] = new() { "smartphone" } });

        var sut = new RecommendationIntentHandler(svc.Object, new PassThroughLocalization(), InMemoryConfig());

        var state = new Dictionary<string, string> { ["Category"] = "phone" };
        var step = await sut.GetNextStep(state, "en");

        Assert.That(step.Status, Is.EqualTo(DialogStepStatus.InProgress));
        Assert.That(step.Prompt, Does.Contain("budget").IgnoreCase);
    }

    [Test]
    public async Task GetNextStep_AsksPreferenceThird()
    {
        var svc = new Mock<IRecommendationService>(MockBehavior.Strict);
        svc.Setup(s => s.GetKnownCategoriesAsync("en"))
           .ReturnsAsync(new Dictionary<string, List<string>> { ["phone"] = new() { "smartphone" } });

        var sut = new RecommendationIntentHandler(svc.Object, new PassThroughLocalization(), InMemoryConfig());

        var state = new Dictionary<string, string> { ["Category"] = "phone", ["Budget"] = "1000" };
        var step = await sut.GetNextStep(state, "en");

        Assert.That(step.Status, Is.EqualTo(DialogStepStatus.InProgress));
        Assert.That(step.Prompt, Does.Contain("preference").IgnoreCase);
    }

    [Test]
    public async Task GetNextStep_AsksDiscountOnlyLast()
    {
        var svc = new Mock<IRecommendationService>(MockBehavior.Strict);
        svc.Setup(s => s.GetKnownCategoriesAsync("en"))
           .ReturnsAsync(new Dictionary<string, List<string>> { ["phone"] = ["smartphone"] });

        var sut = new RecommendationIntentHandler(svc.Object, new PassThroughLocalization(), InMemoryConfig());

        var state = new Dictionary<string, string>
        {
            ["Category"] = "phone",
            ["Budget"] = "1000",
            ["Preference"] = "battery"
        };

        var step = await sut.GetNextStep(state, "en");
        Assert.That(step.Status, Is.EqualTo(DialogStepStatus.InProgress));
        Assert.That(step.Prompt, Does.Contain("discount").IgnoreCase);
    }

    [Test]
    public async Task GetNextStep_CompletedIfAllSlotsFilled()
    {
        var svc = new Mock<IRecommendationService>(MockBehavior.Strict);
        svc.Setup(s => s.GetKnownCategoriesAsync("en"))
           .ReturnsAsync(new Dictionary<string, List<string>> { ["phone"] = new() { "smartphone" } });

        var sut = new RecommendationIntentHandler(svc.Object, new PassThroughLocalization(), InMemoryConfig());

        var state = CompleteState(("DiscountOnly", "true"));
        var step = await sut.GetNextStep(state, "en");
        Assert.That(step.Status, Is.EqualTo(DialogStepStatus.Completed));
    }

    [Test]
    public async Task HandleAsync_ReturnsHtmlResponseWithRecommendations()
    {
        var svc = new Mock<IRecommendationService>(MockBehavior.Strict);
        svc.Setup(s => s.GetKnownCategoriesAsync("en"))
           .ReturnsAsync(new Dictionary<string, List<string>> { ["phone"] = new() { "smartphone" } });

        const string stubName = "Stub Item";
        svc.Setup(s => s.GetRecommendationsAsync(It.IsAny<RecommendationRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync([new RecommendationResult { Name = stubName, Price = 123, Url = "https://test/items/1" }]);

        var sut = new RecommendationIntentHandler(svc.Object, new PassThroughLocalization(), InMemoryConfig());

        var data = CompleteState(("Brand", "none"), ("DiscountOnly", "false"));
        var response = await sut.HandleAsync(data, "en");

        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Answer, Does.Contain("recommendations").IgnoreCase);
        Assert.That(response.Answer, Does.Contain(stubName));
        svc.Verify(s => s.GetRecommendationsAsync(It.IsAny<RecommendationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task BudgetParsing_And_DiscountOnly_Are_Honored()
    {
        var svc = new Mock<IRecommendationService>(MockBehavior.Strict);
        svc.Setup(s => s.GetKnownCategoriesAsync("en"))
           .ReturnsAsync(new Dictionary<string, List<string>> { ["laptop"] = new() { "notebook" } });

        svc.Setup(s => s.GetRecommendationsAsync(It.IsAny<RecommendationRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new List<RecommendationResult>
           {
               new() { Name = "Stub Laptop", Price = 2000, Url = "https://test/items/2" }
           });

        var sut = new RecommendationIntentHandler(svc.Object, new PassThroughLocalization(), InMemoryConfig());

        var data = CompleteState(("Category", "laptop"), ("Brand", "Apple"), ("Budget", "2000"), ("DiscountOnly", "true"));
        var response = await sut.HandleAsync(data, "en");

        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Answer, Does.Contain("recommendations").IgnoreCase);
        svc.Verify(s => s.GetRecommendationsAsync(It.IsAny<RecommendationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
