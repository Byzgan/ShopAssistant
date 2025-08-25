using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using ShopAssistant.Contracts.Enums;
using ShopAssistant.Contracts.Interfaces.Intent;
using ShopAssistant.Contracts.Models.Chat;
using ShopAssistant.Contracts.Models.Intent;
using ShopAssistant.Infrastructure.TextProcessing.Intent;

namespace ShopAssistant.Tests.Infrastructure.TextProcessing.Intent;

[TestFixture]
public class IntentProcessingServiceTests
{
    /// <summary>
    /// Dummy intent handler implementation for testing.
    /// Returns a ChatResponse with the configured answer.
    /// </summary>
    private class DummyIntentHandler(Contracts.Enums.Intent intent, string result) : IIntentHandler
    {
        public Contracts.Enums.Intent Intent { get; } = intent;

        public Task<ChatResponse?> HandleAsync(Dictionary<string, string> collectedData, string language) 
            => Task.FromResult<ChatResponse?>(new ChatResponse { Answer = result });

        // Add this implementation for interface compatibility
        public Task<DialogStepResult> GetNextStep(Dictionary<string, string> collectedData, string language)
        {
            // For testing, always return null (dialog is "complete")
            return Task.FromResult(new DialogStepResult(DialogStepStatus.Completed));
        }
    }

    [Test]
    public void Constructor_Throws_When_Duplicate_Handlers()
    {
        var detector = new Mock<IIntentDetector>().Object;
        var handler1 = new DummyIntentHandler(Contracts.Enums.Intent.ContactSupport, "A");
        var handler2 = new DummyIntentHandler(Contracts.Enums.Intent.ContactSupport, "B");
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<IntentProcessingService>>().Object;

        Assert.Throws<InvalidOperationException>(() => { _ = new IntentProcessingService(detector, [handler1, handler2], logger); });
    }

    [Test]
    public async Task HandleAsync_Returns_Correct_Handler_Response()
    {
        var detector = new Mock<IIntentDetector>().Object;
        var handlerFaq = new DummyIntentHandler(Contracts.Enums.Intent.FAQ, "faq-result");
        var handlerOrder = new DummyIntentHandler(Contracts.Enums.Intent.OrderStatus, "order-result");
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<IntentProcessingService>>().Object;
        var svc = new IntentProcessingService(detector, [handlerFaq, handlerOrder], logger);

        var result1 = await svc.HandleAsync(Contracts.Enums.Intent.FAQ, null!, new Dictionary<string, string>(), "en");
        var result2 = await svc.HandleAsync(Contracts.Enums.Intent.OrderStatus, null!, new Dictionary<string, string>(), "en");

        Assert.That(result1?.Answer, Is.EqualTo("faq-result"));
        Assert.That(result2?.Answer, Is.EqualTo("order-result"));
    }

    [Test]
    public async Task DetectIntentAsync_Delegates_To_Detector()
    {
        var expected = new IntentDetectionResult() { Intent = Contracts.Enums.Intent.OrderStatus };
        var detectorMock = new Mock<IIntentDetector>();
        detectorMock.Setup(x => x.DetectIntentAsync("en", "text")).ReturnsAsync(expected);

        var handler = new DummyIntentHandler(Contracts.Enums.Intent.OrderStatus, "ok");
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<IntentProcessingService>>().Object;
        var svc = new IntentProcessingService(detectorMock.Object, [handler], logger);

        var result = await svc.DetectIntentAsync("en", "text");

        Assert.That(result, Is.SameAs(expected));
        detectorMock.Verify(x => x.DetectIntentAsync("en", "text"), Times.Once);
    }

    [Test]
    public void Constructor_Allows_Empty_Handlers()
    {
        var detector = new Mock<IIntentDetector>().Object;
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<IntentProcessingService>>().Object;
        Assert.DoesNotThrow(() => { _ = new IntentProcessingService(detector, [], logger); });
    }

    [Test]
    public void Constructor_Throws_On_Null_Args()
    {
        var detector = new Mock<IIntentDetector>().Object;
        var handler = new DummyIntentHandler(Contracts.Enums.Intent.FAQ, "resp");
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<IntentProcessingService>>().Object;

        Assert.Throws<ArgumentNullException>(() => { _ = new IntentProcessingService(null!, [handler], logger); });
        Assert.Throws<ArgumentNullException>(() => { _ = new IntentProcessingService(detector, null!, logger); });
    }
}
