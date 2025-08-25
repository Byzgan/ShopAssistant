
using NUnit.Framework;
using Moq;
using ShopAssistant.IntentProcessing.IntentHandlers;
using ShopAssistant.Contracts.Interfaces.Localization;
using ShopAssistant.Contracts.Models.Chat;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShopAssistant.Contracts.Enums;

namespace ShopAssistant.Tests.IntentProcessing.IntentHandlers
{
    [TestFixture]
    public class ContactSupportIntentHandlerTests
    {
        [Test]
        public async Task MultiTurn_Prompts_In_Order()
        {
            var loc = new Mock<ILocalizationService>(MockBehavior.Strict);
            loc.Setup(l => l.GetMessage("MissingIssueType", "en", "contact_support")).Returns("What is the issue?");
            loc.Setup(l => l.GetMessage("MissingDescription", "en", "contact_support")).Returns("Please describe the problem.");
            loc.Setup(l => l.GetMessage("MissingPreferredContact", "en", "contact_support")).Returns("How should we contact you?");
            loc.Setup(l => l.GetMessage("SupportSummary", "en", "contact_support")).Returns("Thanks, we will contact you.");

            var sut = new ContactSupportIntentHandler(loc.Object);

            var s1 = await sut.GetNextStep(new Dictionary<string,string>(), "en");
            Assert.That(s1.Status, Is.EqualTo(DialogStepStatus.InProgress));
            Assert.That(s1.Field, Is.EqualTo("IssueType"));

            var s2 = await sut.GetNextStep(new Dictionary<string,string>{{"IssueType","billing"}}, "en");
            Assert.That(s2.Status, Is.EqualTo(DialogStepStatus.InProgress));
            Assert.That(s2.Field, Is.EqualTo("Description"));

            var s3 = await sut.GetNextStep(new Dictionary<string,string>{{"IssueType","billing"},{"Description","card declined"}}, "en");
            Assert.That(s3.Status, Is.EqualTo(DialogStepStatus.InProgress));
            Assert.That(s3.Field, Is.EqualTo("PreferredContact"));

            var s4 = await sut.GetNextStep(new Dictionary<string,string>{{"IssueType","billing"},{"Description","card declined"},{"PreferredContact","email"}}, "en");
            Assert.That(s4.Status, Is.EqualTo(DialogStepStatus.Completed));
        }
    }
}
