namespace ShopAssistant.Contracts.Interfaces.Intent;

using Enums;

/// <summary>
/// Provides an abstraction for storing and retrieving intent embeddings in memory cache.
/// Designed for singleton DI lifetime.
/// </summary>
public interface IIntentDetectorEmbeddingsCacheService
{
    /// <summary>
    /// Attempts to get the embeddings dictionary from cache.
    /// </summary>
    /// <param name="embeddings">The retrieved embeddings dictionary, if present.</param>
    /// <returns>True if found, otherwise false.</returns>
    bool TryGet(out Dictionary<Intent, Dictionary<string, List<float[]>>>? embeddings);

    /// <summary>
    /// Stores the embeddings dictionary in memory cache.
    /// </summary>
    /// <param name="embeddings">Embeddings to cache.</param>
    void Set(Dictionary<Intent, Dictionary<string, List<float[]>>> embeddings);
}
