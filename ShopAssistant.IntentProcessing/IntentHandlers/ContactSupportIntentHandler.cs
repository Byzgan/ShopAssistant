#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
namespace ShopAssistant.IntentProcessing.IntentHandlers;

using Contracts.Enums;
using Contracts.Interfaces.Intent;
using Contracts.Models.Chat;
using Contracts.Interfaces.Localization;

/// <summary>
/// Handles the ContactSupport intent using multi-turn collected data (issue type, description, preferred contact).
/// Responds in English or Norwegian.
/// </summary>
public class ContactSupportIntentHandler(ILocalizationService localizationService) : IIntentHandler
{
    public Intent Intent => Intent.ContactSupport;
    private const string CacheScope = "contact_support";

    public async Task<ChatResponse?> HandleAsync(Dictionary<string, string> collectedData, string language)
    {
        var lang = language.ToLowerInvariant();

        var issueType = collectedData["IssueType"].Trim();
        var description = collectedData["Description"].Trim();
        var contact = collectedData["PreferredContact"].Trim();

        // Dummy processing logic
        return CreateResponse(lang, "Success", issueType, description, contact);
    }

    /// <summary>
    /// Determines the next required field and prompt for multi-turn dialog.
    /// Returns (field, prompt) or null if all required data is present.
    /// </summary>
    public async Task<DialogStepResult> GetNextStep(Dictionary<string, string> collectedData, string language)
    {
        var lang = language.ToLowerInvariant();

        if (!collectedData.TryGetValue("IssueType", out var issueType) || string.IsNullOrWhiteSpace(issueType))
            return new DialogStepResult(DialogStepStatus.InProgress, "IssueType", GetTranslation(lang, "MissingIssueType"));

        if (!collectedData.TryGetValue("Description", out var description) || string.IsNullOrWhiteSpace(description))
            return new DialogStepResult(DialogStepStatus.InProgress, "Description", GetTranslation(lang, "MissingDescription"));

        if (!collectedData.TryGetValue("PreferredContact", out var contact) || string.IsNullOrWhiteSpace(contact))
            return new DialogStepResult(DialogStepStatus.InProgress, "PreferredContact", GetTranslation(lang, "MissingPreferredContact"));

        // All fields present: dialog complete
        return new DialogStepResult(DialogStepStatus.Completed);
    }

    private ChatResponse CreateResponse(string lang, string messageKey, params object[] args)
    {
        var message = GetTranslation(lang, messageKey);
        if (args is { Length: > 0 })
            message = string.Format(message, args);

        return new ChatResponse
        {
            Answer = message
        };
    }

    private string GetTranslation(string lang, string messageKey)
    {
        return localizationService.GetMessage(messageKey, lang, CacheScope);
    }
}
