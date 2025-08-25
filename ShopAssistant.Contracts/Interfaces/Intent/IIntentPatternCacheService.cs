using ShopAssistant.Contracts.Models.Intent;

namespace ShopAssistant.Contracts.Interfaces.Intent;

/// <summary>
/// Provides an interface for caching and retrieving intent pattern configurations for each language.
/// </summary>
public interface IIntentPatternCacheService
{
    /// <summary>
    /// Gets all intent pattern configurations for a specific language.
    /// </summary>
    /// <param name="language">The language code (e.g., "en").</param>
    /// <returns>A read-only list of intent pattern configurations.</returns>
    IReadOnlyList<IntentPattern>? GetPatternsForLanguage(string language);

    /// <summary>
    /// Sets (or overwrites) all intent pattern configurations for a specific language.
    /// </summary>
    /// <param name="language">The language code.</param>
    /// <param name="patterns">The patterns to store.</param>
    void SetPatternsForLanguage(string language, IReadOnlyList<IntentPattern> patterns);

    /// <summary>
    /// Loads or reloads all intent patterns/semantic phrases from disk.
    /// </summary>
    Task InitializeCacheAsync();
}