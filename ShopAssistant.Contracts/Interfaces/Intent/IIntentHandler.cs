using ShopAssistant.Contracts.Models.Chat;

namespace ShopAssistant.Contracts.Interfaces.Intent;

/// <summary>
/// Handler for a specific user intent. Supports multi-turn scenarios.
/// </summary>
public interface IIntentHandler
{
    /// <summary>
    /// The intent this handler processes.
    /// </summary>
    Enums.Intent Intent { get; }

    /// <summary>
    /// Handles the intent using the collected scenario data (multi-turn support).
    /// </summary>
    /// <param name="collectedData">All user answers to scenario fields, key-value.</param>
    /// <param name="language">Language code.</param>
    /// <returns>Structured chat response.</returns>
    Task<ChatResponse?> HandleAsync(Dictionary<string, string> collectedData, string language);

    /// <summary>
    /// Returns the next required step/slot for the dialog, based on what is already collected.
    /// If all required fields are filled, returns null.
    /// </summary>
    /// <param name="collectedData">The data collected so far.</param>
    /// <param name="language">Language for prompt.</param>
    /// <returns></returns>
    Task<DialogStepResult> GetNextStep(Dictionary<string, string> collectedData, string language);
}