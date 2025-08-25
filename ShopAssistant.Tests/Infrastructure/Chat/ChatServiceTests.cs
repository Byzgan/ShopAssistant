using NUnit.Framework;
using Moq;
using Microsoft.Extensions.Logging;
using ShopAssistant.Infrastructure.Chat;
using ShopAssistant.Contracts.Interfaces.KnowledgeBase;
using ShopAssistant.Contracts.Interfaces.Intent;
using ShopAssistant.Contracts.Interfaces.Chat;
using ShopAssistant.Contracts.Interfaces.User;
using ShopAssistant.Contracts.Interfaces.Localization;
using ShopAssistant.Contracts.Interfaces.Analytics;
using ShopAssistant.Contracts.Models.Chat;
using ShopAssistant.Contracts.Models.KnowledgeBase;
using ShopAssistant.Contracts.Models.User;
using ShopAssistant.Contracts.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShopAssistant.Tests.Infrastructure.Chat;

[TestFixture]
public class ChatServiceTests
{
    [Test]
    public async Task FaqHit_Returns_Answer_And_Logs()
    {
        var kb = new Mock<IKnowledgeBaseService>(MockBehavior.Strict);
        var intent = new Mock<IIntentProcessingService>(MockBehavior.Strict);
        var perm = new Mock<ITopicRolePermissionProvider>(MockBehavior.Strict);
        var ctx = new Mock<IUserChatContextService>(MockBehavior.Strict);
        var userCtx = new Mock<IUserContext>(MockBehavior.Strict);
        var loc = new Mock<ILocalizationService>(MockBehavior.Strict);
        var analytics = new Mock<IAnalyticsRepository>(MockBehavior.Strict);
        var logger = new Mock<ILogger<ChatService>>(MockBehavior.Loose);

        var user = new User { Id = 1, Role = UserRole.User, ExternalSystem = "web", UniqueKey = "u1" };
        userCtx.SetupGet(u => u.CurrentUser).Returns(user);

        var allowed = new HashSet<KnowledgeTopic>{ KnowledgeTopic.Shipping, KnowledgeTopic.Order };
        perm.Setup(p => p.GetAllowedTopicsForRole(UserRole.User)).ReturnsAsync(allowed);

        kb.Setup(k => k.FindCachedAnswerAsync("where is my order", "en", allowed))
            .ReturnsAsync(new KnowledgeItem { Id = 42, Language = "en", Topic = KnowledgeTopic.Shipping, Answer = "Track it here", Questions =
                ["q"]
            });

        analytics.Setup(a => a.SaveFaqQueryLogAsync(It.IsAny<Contracts.Models.Analytics.FaqQueryLogEntry>()))
            .Returns(Task.CompletedTask);

        ctx.Setup(c => c.AddUserMessageAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var sut = new ChatService(kb.Object, intent.Object, perm.Object, ctx.Object, userCtx.Object, loc.Object, analytics.Object, logger.Object);

        var res = await sut.ProcessMessageAsync(new ChatRequest{ Message="where is my order", Language="en" });

        Assert.That(res, Is.Not.Null);
        Assert.That(res!.Answer, Is.EqualTo("Track it here"));

        kb.VerifyAll();
        perm.VerifyAll();
        analytics.VerifyAll();
        intent.VerifyNoOtherCalls();
    }
}