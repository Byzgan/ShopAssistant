using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using ShopAssistant.Contracts.Interfaces.Localization;

namespace ShopAssistant.Infrastructure.Localization;

/// <summary>
/// Loads and caches localization messages from JSON files into IMemoryCache.
/// Supports both global and intent-specific localization files.
/// File naming convention: {scope}.messages.{lang}.json or {scope}.intent.{lang}.json.
/// </summary>
public class LocalizationService(IConfiguration configuration, ILogger<LocalizationService> logger, IMemoryCache memoryCache) : ILocalizationService
{
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly ILogger<LocalizationService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IMemoryCache _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    private const string CacheKeyPrefix = "Localization:";

    /// <summary>
    /// Loads all localization files from disk into the memory cache on startup.
    /// Each file is mapped to a cache key: "Localization:{scope}:{lang}".
    /// </summary>
    public async Task InitializeCacheAsync()
    {
        var languages = _configuration.GetSection("Languages:Supported").Get<string[]>();
        var localizationFilePath = _configuration.GetValue<string>("LocalizationFilePath");

        if (languages == null || languages.Length == 0)
            throw new InvalidOperationException("'Languages' not configured in appsettings.json.");
        if (string.IsNullOrWhiteSpace(localizationFilePath))
            throw new InvalidOperationException("'LocalizationFilePath' is missing or empty in appsettings.json.");

        // Load all supported message file patterns
        var files = Directory.GetFiles(localizationFilePath, "global.messages.*.json", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(localizationFilePath, "*.intent.*.json", SearchOption.TopDirectoryOnly))
            .Concat(Directory.GetFiles(localizationFilePath, "intents.*.json", SearchOption.TopDirectoryOnly))
            .Concat(Directory.GetFiles(localizationFilePath, "product_categories.*.json", SearchOption.TopDirectoryOnly))
            .Concat(Directory.GetFiles(localizationFilePath, "knowledge_topics.*.json", SearchOption.TopDirectoryOnly))
            .ToArray();

        foreach (var filePath in files)
        {
            var fileName = Path.GetFileName(filePath);
            if (string.IsNullOrWhiteSpace(fileName)) 
                continue;

            // Expected: {scope}.*.{lang}.json
            var parts = fileName.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                _logger.LogWarning("Skipping localization file with unexpected naming: {File}", fileName);
                continue;
            }
            var scope = parts[0];    // "product_search" or "global" or "admin" etc.
            var lang =  parts[^2];   // "en", "no", etc.
            var cacheKey = $"{CacheKeyPrefix}{scope}:{lang}";

            try
            {
                await using var stream = File.OpenRead(filePath);
                var json = await new StreamReader(stream).ReadToEndAsync();
                var messages = JsonSerializer.Deserialize<Dictionary<string, string>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (messages != null)
                {
                    _memoryCache.Set(cacheKey, messages);
                    _logger.LogInformation("Loaded localization for {Scope} ({Lang}) from {File}", scope, lang, fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading localization file: {File}", fileName);
            }
        }
    }

    /// <summary>
    /// Returns a localized message by key, language, and scope (from the first word of filename).
    /// Falls back to English within the same scope if not found for the requested language.
    /// </summary>
    /// <param name="key">Message key to look up.</param>
    /// <param name="language">Language code, e.g., "en", "no".</param>
    /// <param name="scope">Scope, e.g., "product_search", "global", from filename.</param>
    /// <returns>The localized message, or "Unknown error." if not found.</returns>
    public string GetMessage(string key, string language, string scope)
    {
        var dictKey = $"{CacheKeyPrefix}{scope}:{language}";
        if (_memoryCache.TryGetValue(dictKey, out Dictionary<string, string>? dict) && dict is not null && dict.TryGetValue(key, out var localized))
            return localized;

        // Fallback to English for this scope only
        var dictKeyEn = $"{CacheKeyPrefix}{scope}:en";
        if (_memoryCache.TryGetValue(dictKeyEn, out Dictionary<string, string>? dictEn) && dictEn is not null && dictEn.TryGetValue(key, out var enLocalized))
        {
            _logger.LogDebug("Falling back to English for key: {Key} (scope: {Scope})", key, scope);
            return enLocalized;
        }

        _logger.LogWarning("Missing localization for key '{Key}' in '{Lang}' (scope: {Scope}) and fallback.", key, language, scope);
        return "Unknown error.";
    }
}
