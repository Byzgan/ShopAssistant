namespace ShopAssistant.Contracts.Interfaces.Localization;

/// <summary>
/// Defines a contract for retrieving localized messages based on a message key and language code.
/// Implementations may use hardcoded dictionaries, resource files, databases, or external services for localization.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Returns a localized string for a given message key and language code.
    /// </summary>
    /// <param name="key">Message key (e.g., "request_processed")</param>
    /// <param name="language">Language code (e.g., "en", "no")</param>
    /// <param name="scope">Scope of the message (e.g., "global", "user")</param>
    /// <returns>Localized message string, or a fallback text if not found.</returns>
    string GetMessage(string key, string language, string scope);


    /// <summary>
    /// Initializes the in-memory cache from disk. Should be called on application startup.
    /// </summary>
    Task InitializeCacheAsync();
}