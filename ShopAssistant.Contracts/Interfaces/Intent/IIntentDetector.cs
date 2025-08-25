using ShopAssistant.Contracts.Models.Intent;

namespace ShopAssistant.Contracts.Interfaces.Intent;

/// <summary>
/// Interface for intent detection in user queries.
/// </summary>
public interface IIntentDetector
{
    /// <summary>
    /// Detects the intent of the user's message and determines if clarification is needed.
    /// </summary>
    /// <param name="language">Language code ("en", "no", ...).</param>
    /// <param name="message">User message text.</param>
    /// <returns>Detected intent, clarification needs, and follow-up message.</returns>
    Task<IntentDetectionResult> DetectIntentAsync(string language, string message);
}