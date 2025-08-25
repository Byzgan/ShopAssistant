namespace ShopAssistant.Infrastructure.TextProcessing.Intent;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShopAssistant.Contracts.Interfaces.Intent;
using ShopAssistant.Contracts.Models.Intent;

/// <summary>
/// Caches and initializes intent pattern (semantic phrase) configurations for each language using IMemoryCache.
/// </summary>
public class MemoryIntentPatternCacheService(IConfiguration configuration, IMemoryCache memoryCache, ILogger<MemoryIntentPatternCacheService> logger) : IIntentPatternCacheService
{
    private readonly string _configDirectory = configuration["IntentPatternsPath"] ?? throw new InvalidOperationException("IntentPatternsPath not set in configuration.");
    private const string CacheKeyPrefix = "IntentPatterns";
    private readonly ILogger<MemoryIntentPatternCacheService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IMemoryCache _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));

    public IReadOnlyList<IntentPattern>? GetPatternsForLanguage(string language) => _memoryCache.TryGetValue(GetCacheKey(language), out List<IntentPattern>? patterns) ? patterns : [];

    public void SetPatternsForLanguage(string language, IReadOnlyList<IntentPattern> patterns) => _memoryCache.Set(GetCacheKey(language), patterns);

    /// <summary>
    /// Loads all intent pattern files and puts the result in IMemoryCache.
    /// </summary>
    public async Task InitializeCacheAsync()
    {
        var basePath = Directory.GetCurrentDirectory();
        var intentPatternFolderPath = Path.Combine(basePath, "..", _configDirectory);

        var allPatterns = await IntentPatternFileLoader.LoadAllIntentPatternsAsync(intentPatternFolderPath, _logger);

        foreach (var (language, patterns) in allPatterns)
        {
            SetPatternsForLanguage(language, patterns);
            _logger.LogInformation("Loaded {Count} intent patterns for {Language}.", patterns.Count, language);
        }
    }

    private static string GetCacheKey(string language) => $"{CacheKeyPrefix}:{language}";
}
