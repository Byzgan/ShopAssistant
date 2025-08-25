using Microsoft.Extensions.VectorData;

namespace ShopAssistant.Contracts.Interfaces.TextProcessing;

/// <summary>
/// Abstraction for vector storage and similarity search operations.
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Finds the k nearest neighbors to the given query vector, returning similarity scores (1 - distance).
    /// </summary>
    /// <param name="query">Query tuple (index, vector).</param>
    /// <param name="k">Number of neighbors to retrieve.</param>
    /// <returns>List of nearest neighbors with similarity scores.</returns>
    IReadOnlyList<VectorSearchResult<(int, float)>> FindNearest((int, float[]) query, int k);

    /// <summary>
    /// Gets the total number of vectors stored in the index.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Returns a byte array containing the serialized ANN index.
    /// </summary>
    /// <returns>Serialized index as a byte array.</returns>
    byte[] GetSerializedIndex();
}
