namespace ShopAssistant.Contracts.Interfaces.Intent;

using User;
using Enums;
using ShopAssistant.Contracts.Models.Chat;
using ShopAssistant.Contracts.Models.Intent;

/// <summary>
/// Facade interface for user intent detection and handling.
/// </summary>
public interface IIntentProcessingService
{
    /// <summary>
    /// Detects intent from user message.
    /// </summary>
    /// <param name="language">Language code.</param>
    /// <param name="message">User question.</param>
    /// <returns>Detected intent and metadata.</returns>
    Task<IntentDetectionResult> DetectIntentAsync(string language, string message);

    /// <summary>
    /// Calls the intent handler for the specified intent.
    /// </summary>
    /// <param name="intent">Intent type.</param>
    /// <param name="userContext">User context.</param>
    /// <param name="collectedData">Collected answers for scenario fields.</param>
    /// <param name="language">Language code.</param>
    /// <returns>Response string, or null.</returns>
    Task<ChatResponse?> HandleAsync(Intent intent, IUserContext userContext, Dictionary<string, string> collectedData, string language);

    /// <summary>
    /// Returns the intent handler for the given intent, or null if none exists.
    /// </summary>
    IIntentHandler? GetHandlerForIntent(Intent intent);
}