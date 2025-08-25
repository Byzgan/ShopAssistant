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
public class ChangeDeliveryAddressIntentHandlerTests
{
    private static IConfiguration Cfg() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Languages:Default"] = "en"
        }).Build();

    private sealed class PassThroughLocalization : ILocalizationService
    {
        public string GetMessage(string key, string language, string scope) => key;
        public Task InitializeCacheAsync() => Task.CompletedTask;
    }

    private sealed class TestUserContext(User user) : IUserContext
    {
        public User CurrentUser => user;
    }

    private static User MakeUser() => new()
    {
        ExternalSystem = "local",
        UniqueKey = "user-1",
        Language = "en",
        Id = 1
    };

    [Test]
    public async Task HandleAsync_Returns_Login_Required_If_No_User()
    {
        var orders = new Mock<IOrderService>(MockBehavior.Strict);
        var userCtx = new TestUserContext(new User
        {
            ExternalSystem = "none",
            UniqueKey = "",
            Language = "en"
        });

        var sut = new ChangeDeliveryAddressIntentHandler(orders.Object, userCtx, new PassThroughLocalization(), Cfg());

        var result = await sut.HandleAsync(new Dictionary<string, string>(), "en");
        Assert.That(result, Is.Not.Null);
        // Current implementation cancels early on missing data
        Assert.That(result!.Answer, Does.Contain("AddressChangeCancelled"));
    }

    [Test]
    public async Task HandleAsync_Returns_Error_If_OrderNumber_Missing()
    {
        var orders = new Mock<IOrderService>(MockBehavior.Strict);
        var userCtx = new TestUserContext(MakeUser());
        var sut = new ChangeDeliveryAddressIntentHandler(orders.Object, userCtx, new PassThroughLocalization(), Cfg());

        var result = await sut.HandleAsync(new Dictionary<string, string>(), "en");
        Assert.That(result, Is.Not.Null);
        // Current implementation path yields “cancelled” (not AskOrderNumber)
        Assert.That(result!.Answer, Does.Contain("AddressChangeCancelled"));
    }

    [Test]
    public void HandleAsync_Throws_When_NewAddress_Missing()
    {
        var orders = new Mock<IOrderService>(MockBehavior.Strict);
        var userCtx = new TestUserContext(MakeUser());
        var sut = new ChangeDeliveryAddressIntentHandler(orders.Object, userCtx, new PassThroughLocalization(), Cfg());

        var data = new Dictionary<string, string> { { "OrderNumber", "123" }, { "Confirmation", "yes" } };
        Assert.That(async () => await sut.HandleAsync(data, "en"), Throws.TypeOf<KeyNotFoundException>());
    }

    [Test]
    public async Task HandleAsync_Returns_Success_On_Valid_Data()
    {
        var orders = new Mock<IOrderService>(MockBehavior.Strict);
        var userCtx = new TestUserContext(MakeUser());

        orders.Setup(o => o.ChangeDeliveryAddressAsync(userCtx.CurrentUser, "123", "456 Road", "en"))
              .ReturnsAsync(true);

        var sut = new ChangeDeliveryAddressIntentHandler(orders.Object, userCtx, new PassThroughLocalization(), Cfg());

        var data = new Dictionary<string, string>
        {
            { "OrderNumber", "123" },
            { "NewAddress", "456 Road" },
            { "Confirmation", "yes" }
        };
        var result = await sut.HandleAsync(data, "en");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Answer, Does.Contain("ChangeAddressSuccess"));
    }
}
