namespace ShopAssistant.Infrastructure.TextProcessing.SemanticSearch.Embeddings;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Collections.Concurrent;

/// <summary>
/// Provides in-memory caching for language-specific EmbeddingsStore, HnswVectorStore, and QuestionAnswerMappingStore instances.
/// Each language gets its own embeddings and ANN index, which are loaded from disk once and reused.
/// </summary>
public class EmbeddingIndexCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<EmbeddingIndexCacheService> _logger;
    private readonly string _embeddingsBasePath;
    private readonly string[] _languages;

    public EmbeddingIndexCacheService(IMemoryCache memoryCache, IConfiguration configuration, ILogger<EmbeddingIndexCacheService> logger)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var basePath = Directory.GetCurrentDirectory();
        _embeddingsBasePath = Path.Combine(basePath, "..", configuration["EmbeddingsPath"] ?? throw new InvalidOperationException());

        _languages = configuration.GetSection("Languages:Supported").Get<string[]>() ?? throw new InvalidOperationException("Languages must be specified in configuration under 'Languages' section.");
    }

    /// <summary>
    /// Gets the EmbeddingsStore for a specific language from cache, loading from disk if needed.
    /// Throws if the service is disposed or the language is invalid.
    /// </summary>
    public EmbeddingsStore GetKnowledgeBaseEmbeddingsStore(string language)
    {
        var normalizedLanguage = ValidateAndNormalizeLanguage(language);
        var key = CacheKeys.KnowledgeBaseEmbeddings(normalizedLanguage);
        var path = Path.Combine(_embeddingsBasePath, $"kb_embeddings_{normalizedLanguage}.bin");

        EnsureFileExists(path, $"Embeddings file not found for language '{normalizedLanguage}'.");

        return _memoryCache.GetOrCreate(key, entry =>
        {
            entry.Priority = CacheItemPriority.Normal;
            return new EmbeddingsStore(path);
        })!;
    }

    /// <summary>
    /// Gets the HnswVectorStore for a specific language from cache, loading from disk if needed.
    /// </summary>
    public HnswVectorStore GetKnowledgeBaseVectorStore(string language)
    {
        var normalizedLanguage = ValidateAndNormalizeLanguage(language);
        var key = CacheKeys.KnowledgeBaseIndex(normalizedLanguage);
        var path = Path.Combine(_embeddingsBasePath, $"kb_index_{normalizedLanguage}.hnsw");

        EnsureFileExists(path, $"HnswVectorStore file not found for language '{normalizedLanguage}'.");

        return _memoryCache.GetOrCreate(key, entry =>
        {
            entry.Priority = CacheItemPriority.Normal;
            var embeddingsStore = GetKnowledgeBaseEmbeddingsStore(normalizedLanguage);
            var vectorSize = embeddingsStore.Embeddings.Count > 0 
                ? embeddingsStore.Embeddings[0].Length 
                : throw new InvalidOperationException($"No embeddings loaded for language '{language}'.");

            return new HnswVectorStore(embeddingsStore.Embeddings, vectorSize, path);
        })!;
    }

    /// <summary>
    /// Gets the QuestionAnswerMappingStore for a specific language from cache, loading from disk if needed.
    /// </summary>
    public QuestionAnswerMappingStore GetKnowledgeBaseMappingStore(string language)
    {
        var normalizedLanguage = ValidateAndNormalizeLanguage(language);
        var key = CacheKeys.KnowledgeBaseMapping(normalizedLanguage);
        var path = Path.Combine(_embeddingsBasePath, $"kb_meta_{normalizedLanguage}.json");

        EnsureFileExists(path, $"QuestionAnswerMappingStore file not found for language '{normalizedLanguage}'.");

        return _memoryCache.GetOrCreate(key, entry =>
        {
            entry.Priority = CacheItemPriority.Normal;
            return new QuestionAnswerMappingStore(path);
        })!;
    }

    /// <summary>
    /// Asynchronously preloads all language-specific embedding, ANN, and mapping stores into cache,
    /// parallelizing across languages for performance. Throws on any failure.
    /// </summary>
    public async Task InitializeCacheAsync()
    {
        if (_languages.Length == 0)
        {
            _logger.LogWarning("No languages specified in configuration for ANN cache preload.");
            return;
        }

        var failedLanguages = new ConcurrentBag<(string Language, Exception Exception)>();
        var tasks = new List<Task>();

        // Use explicit parallelization for better async semantics (not Parallel.ForEach)
        foreach (var language in _languages)
        {
            var lang = CacheKeys.Normalize(language);

            tasks.Add(Task.Run(() =>
            {
                try
                {
                    // These may do file IO or CPU work, so parallelize
                    GetKnowledgeBaseEmbeddingsStore(lang);
                    GetKnowledgeBaseVectorStore(lang);
                    GetKnowledgeBaseMappingStore(lang);

                    _logger.LogInformation("Preloaded ANN index and embeddings for language: {Language}", lang);
                }
                catch (Exception ex)
                {
                    failedLanguages.Add((lang, ex));
                    _logger.LogError(ex, "Failed to preload ANN index and embeddings for language: {Language}", lang);
                }
            }));
        }

        await Task.WhenAll(tasks);

        if (!failedLanguages.IsEmpty)
        {
            var errorReport = string.Join(", ", failedLanguages.Select(f => $"{f.Language}: {f.Exception.Message}"));
            _logger.LogError("ANN preload failed for {Count} languages: {Report}", failedLanguages.Count, errorReport);
            throw new AggregateException("Preload failed for one or more languages", failedLanguages.Select(f => f.Exception));
        }

        _logger.LogInformation("Successfully preloaded ANN index and embeddings for all languages: {Languages}", string.Join(", ", _languages));
    }



    private static string ValidateAndNormalizeLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("Language must be provided.", nameof(language));
        return CacheKeys.Normalize(language);
    }

    private static void EnsureFileExists(string path, string errorMessage)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(errorMessage, path);
    }
}

internal static class CacheKeys
{
    public static string KnowledgeBaseEmbeddings(string language) => $"kb_embeddings_{Normalize(language)}";
    public static string KnowledgeBaseIndex(string language) => $"kb_index_{Normalize(language)}";
    public static string KnowledgeBaseMapping(string language) => $"kb_mapping_{Normalize(language)}";

    public static string Normalize(string language) => language.Trim().ToLowerInvariant();
}
