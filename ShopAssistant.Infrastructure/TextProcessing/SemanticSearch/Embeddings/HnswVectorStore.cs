namespace ShopAssistant.Infrastructure.TextProcessing.SemanticSearch.Embeddings;

using HNSW.Net;
using Microsoft.Extensions.VectorData;
using ShopAssistant.Contracts.Interfaces.TextProcessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

/// <summary>
/// HNSW-based persistent vector store.
/// - The order of <paramref name="embeddings"/> defines node IDs (QIDs) 0..N-1.
/// - On deserialization you MUST pass the *same vectors in the same order*, or results will map to wrong items.
/// - Distance: cosine distance normalized to [0,1] = (1 - cos)/2.
/// </summary>
public sealed class HnswVectorStore : IVectorStore
{
    private readonly SmallWorld<float[], float> _index;
    private readonly Lock _lock = new();

    public HnswVectorStore(IReadOnlyList<float[]> embeddings, int dimensions, string? indexFilePath = null)
    {
        if (embeddings is null) throw new ArgumentNullException(nameof(embeddings));
        if (dimensions <= 0) throw new ArgumentOutOfRangeException(nameof(dimensions));

        for (int i = 0; i < embeddings.Count; i++)
        {
            var v = embeddings[i];
            if (v is null || v.Length != dimensions)
                throw new ArgumentException($"Embedding at index {i} has invalid length (expected {dimensions}).");
        }

        var parameters = new SmallWorld<float[], float>.Parameters
        {
            M = 10, // Maximum neighbors per node in each layer (controls search quality/memory)
            LevelLambda = 1.0 / Math.Log(10), // Level distribution for HNSW graph (usually auto)
            NeighbourHeuristic = NeighbourSelectionHeuristic.SelectHeuristic, // Selection strategy for neighbors
            ConstructionPruning = 200, // Thoroughness during construction
            ExpandBestSelection = false,
            KeepPrunedConnections = false,
            EnableDistanceCacheForConstruction = true,
            InitialDistanceCacheSize = 1_048_576,
            InitialItemsSize = 1024
        };

        if (!string.IsNullOrEmpty(indexFilePath) && File.Exists(indexFilePath) && new FileInfo(indexFilePath).Length > 0)
        {
            using var fs = File.OpenRead(indexFilePath);
            (_index, _) = SmallWorld<float[], float>.DeserializeGraph(
                items: new List<float[]>(embeddings), // preserve exact order
                distance: CosineDistance,
                generator: DefaultRandomGenerator.Instance,
                stream: fs,
                threadSafe: true
            );

            var cnt = _index.Items?.Count ?? -1;
            if (cnt != embeddings.Count)
                throw new InvalidOperationException($"HNSW graph count ({cnt}) != embeddings count ({embeddings.Count}). Ensure identical list & order on load.");
        }
        else
        {
            _index = new SmallWorld<float[], float>(
                distance: CosineDistance,
                generator: DefaultRandomGenerator.Instance,
                parameters: parameters,
                threadSafe: true
            );

            _index.AddItems(new List<float[]>(embeddings)); // IDs follow this order
        }
    }

    public void SaveToFile(string indexFilePath)
    {
        if (string.IsNullOrWhiteSpace(indexFilePath))
            throw new ArgumentException("Index file path must be provided.", nameof(indexFilePath));

        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(indexFilePath))!);
            using var fs = File.Open(indexFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            _index.SerializeGraph(fs);
        }
    }

    public byte[] GetSerializedIndex()
    {
        lock (_lock)
        {
            using var ms = new MemoryStream();
            _index.SerializeGraph(ms);
            return ms.ToArray();
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _index.Items?.Count ?? 0;
            }
        }
    }

    /// <summary>
    /// KNN over cosine distance in [0,1]; returns similarity = 1 - distance.
    /// Results are sorted by ascending distance before conversion.
    /// </summary>
    public IReadOnlyList<VectorSearchResult<(int, float)>> FindNearest((int, float[]) query, int k)
    {
        if (query.Item2 is null) throw new ArgumentNullException(nameof(query));
        if (k <= 0) throw new ArgumentOutOfRangeException(nameof(k));

        var results = new List<VectorSearchResult<(int, float)>>();

        lock (_lock)
        {
            var found = _index.KNNSearch(query.Item2, k);

            foreach (var res in found.OrderBy(r => r.Distance))
            {
                // record tuple: (QID, Distance)
                results.Add(new VectorSearchResult<(int, float)>((res.Id, res.Distance), 1.0f - res.Distance));
            }
        }

        return results;
    }

    // ----- Distance: cosine normalized to [0,1] = (1 - cos)/2 -----------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float CosineDistance(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length != b.Length)
            throw new ArgumentException("Vectors must be non-null and of the same length.");

        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++)
        {
            var ai = a[i]; var bi = b[i];
            dot += ai * bi;
            na += ai * ai;
            nb += bi * bi;
        }

        if (na == 0 && nb == 0) return 0f; // identical zero vectors
        if (na == 0 || nb == 0) return 1f; // one-zero => maximal distance

        var cos = dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        if (cos > 1) cos = 1;
        else if (cos < -1) cos = -1;

        return (float)((1.0 - cos) * 0.5);
    }

#if DEBUG
    /// <summary>
    /// DEBUG helper: brute-force exact kNN using the same distance. Not used in production.
    /// </summary>
    public IReadOnlyList<VectorSearchResult<(int, float)>> FindNearestExact(float[] query, int k)
    {
        if (query is null) throw new ArgumentNullException(nameof(query));
        if (k <= 0) throw new ArgumentOutOfRangeException(nameof(k));

        List<VectorSearchResult<(int, float)>> results;
        lock (_lock)
        {
            var items = _index.Items ?? throw new InvalidOperationException("Index has no items.");
            var tmp = new List<(int Id, float Dist)>(items.Count);

            for (int id = 0; id < items.Count; id++)
                tmp.Add((id, CosineDistance(items[id], query)));

            results = tmp.OrderBy(t => t.Dist)
                         .Take(k)
                         .Select(t => new VectorSearchResult<(int, float)>((t.Id, t.Dist), 1.0f - t.Dist))
                         .ToList();
        }
        return results;
    }
#endif
}
