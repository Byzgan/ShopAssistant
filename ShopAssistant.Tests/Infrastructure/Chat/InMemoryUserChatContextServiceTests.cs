using NUnit.Framework;
using System.Threading.Tasks;
using ShopAssistant.Infrastructure.Chat;
using ShopAssistant.Contracts.Models.Intent;
using ShopAssistant.Contracts.Enums;

namespace ShopAssistant.Tests.Infrastructure.Chat;

[TestFixture]
public class InMemoryUserChatContextServiceTests
{
    [Test]
    public async Task History_Fifo_And_Clear_Works_Per_User()
    {
        var sut = new InMemoryUserChatContextService();

        await sut.AddUserMessageAsync("u1", "hello");
        await sut.AddUserMessageAsync("u1", "world");
        await sut.AddUserMessageAsync("u2", "other");

        var h1 = await sut.GetUserHistoryAsync("u1");
        var h2 = await sut.GetUserHistoryAsync("u2");

        Assert.That(h1, Is.Not.Null);
        Assert.That(h1!.Count, Is.EqualTo(2));
        Assert.That(h1[0], Is.EqualTo("hello"));
        Assert.That(h1[1], Is.EqualTo("world"));

        Assert.That(h2, Is.Not.Null);
        Assert.That(h2!.Count, Is.EqualTo(1));
        Assert.That(h2[0], Is.EqualTo("other"));

        await sut.ClearUserHistoryAsync("u1");
        var cleared = await sut.GetUserHistoryAsync("u1");
        Assert.That(cleared, Is.Not.Null);
        Assert.That(cleared!.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task Pending_Context_Set_Get_Clear_Works()
    {
        var sut = new InMemoryUserChatContextService();

        var ctx = new PendingIntentContext
        {
            Intent = Intent.Recommend,
            CurrentField = "Category",
            CurrentPrompt = "Which category?"
        };

        await sut.SetPendingIntentAsync("u1", ctx);
        var got1 = await sut.GetPendingIntentAsync("u1");
        var got2 = await sut.GetPendingIntentAsync("u2");

        Assert.That(got1, Is.Not.Null);
        Assert.That(got1!.Intent, Is.EqualTo(Intent.Recommend));
        Assert.That(got2, Is.Null);

        await sut.SetPendingIntentAsync("u1", null);
        var cleared = await sut.GetPendingIntentAsync("u1");
        Assert.That(cleared, Is.Null);
    }
}