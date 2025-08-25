using Microsoft.Extensions.Caching.Memory;
using ShopAssistant.Contracts.Enums;
using ShopAssistant.Contracts.Interfaces.Intent;

namespace ShopAssistant.IntentProcessing.IntentDetectors;

/// <summary>
/// In-memory implementation of intent embeddings cache service.
/// Uses Microsoft.Extensions.Caching.Memory for fast storage during application lifetime.
/// </summary>
public class IntentDetectorEmbeddingsCacheService(IMemoryCache memoryCache) : IIntentDetectorEmbeddingsCacheService
{
    private const string CacheKey = "IntentDetectorEmbeddingsCache";

    /// <inheritdoc/>
    public bool TryGet(out Dictionary<Intent, Dictionary<string, List<float[]>>>? embeddings)
    {
        return memoryCache.TryGetValue(CacheKey, out embeddings);
    }

    /// <inheritdoc/>
    public void Set(Dictionary<Intent, Dictionary<string, List<float[]>>> embeddings)
    {
        // Never remove this entry except on explicit memory pressure
        memoryCache.Set(CacheKey, embeddings, new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.NeverRemove
        });
    }
}