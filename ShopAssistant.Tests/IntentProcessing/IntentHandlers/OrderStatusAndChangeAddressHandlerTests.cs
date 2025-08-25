// ShopAssistant.Tests/IntentProcessing/IntentHandlers/OrderStatusAndChangeAddressHandlerTests.cs
namespace ShopAssistant.Tests.IntentProcessing.IntentHandlers;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;
using ShopAssistant.Contracts.Interfaces.Integrations;
using ShopAssistant.Contracts.Interfaces.Localization;
using ShopAssistant.Contracts.Interfaces.User;
using ShopAssistant.Contracts.Models.User;
using ShopAssistant.IntentProcessing.IntentHandlers;

[TestFixture]
public class OrderStatusAndChangeAddressHandlerTests
{
    private static IConfiguration Cfg() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Languages:Default"] = "en"
        }).Build();

    private static Mock<ILocalizationService> Loc()
    {
        var loc = new Mock<ILocalizationService>(MockBehavior.Loose);
        loc.Setup(l => l.InitializeCacheAsync()).Returns(Task.CompletedTask);
        loc.Setup(l => l.GetMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
           .Returns<string, string, string>((k, _, __) => k);
        return loc;
    }

    private static User MakeUser() => new()
    {
        ExternalSystem = "local",
        UniqueKey = "user-7",
        Language = "en",
        Id = 7
    };

    [Test]
    public async Task OrderStatus_LatestOrder_Flow_Works()
    {
        var order = new Mock<IOrderService>(MockBehavior.Strict);
        var userCtx = new Mock<IUserContext>(MockBehavior.Strict);
        var loc = Loc();

        var user = MakeUser();
        userCtx.SetupGet(u => u.CurrentUser).Returns(user);
        order.Setup(o => o.GetLatestOrderStatusAsync(user, "en")).ReturnsAsync("Shipped");

        var sut = new OrderStatusIntentHandler(order.Object, userCtx.Object, loc.Object, Cfg());

        var data = new Dictionary<string, string> { ["Latest"] = "true" };
        var res = await sut.HandleAsync(data, "en");

        Assert.That(res, Is.Not.Null);
        // Your localization returns the key; do not expect “Shipped” text embedded.
        Assert.That(res!.Answer, Does.Contain("LatestOrderStatus"));
    }

    [Test]
    public async Task ChangeAddress_Success_And_Error_Are_Localized()
    {
        var order = new Mock<IOrderService>(MockBehavior.Strict);
        var userCtx = new Mock<IUserContext>(MockBehavior.Strict);
        var loc = Loc();

        var user = new User
        {
            ExternalSystem = "local",
            UniqueKey = "user-1",
            Language = "en",
            Id = 1
        };
        userCtx.SetupGet(u => u.CurrentUser).Returns(user);

        order.SetupSequence(o => o.ChangeDeliveryAddressAsync(user, "123", "Addr", "en"))
             .ReturnsAsync(true)
             .ReturnsAsync(false);

        var handler = new ChangeDeliveryAddressIntentHandler(order.Object, userCtx.Object, loc.Object, Cfg());

        var ok = await handler.HandleAsync(new Dictionary<string, string>
        {
            ["OrderNumber"] = "123",
            ["NewAddress"] = "Addr",
            ["Confirmation"] = "yes"
        }, "en");
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.Answer, Does.Contain("ChangeAddressSuccess"));

        var fail = await handler.HandleAsync(new Dictionary<string, string>
        {
            ["OrderNumber"] = "123",
            ["NewAddress"] = "Addr",
            ["Confirmation"] = "yes"
        }, "en");
        Assert.That(fail, Is.Not.Null);
        Assert.That(fail!.Answer, Does.Contain("ChangeAddressFailed"));
    }
}
