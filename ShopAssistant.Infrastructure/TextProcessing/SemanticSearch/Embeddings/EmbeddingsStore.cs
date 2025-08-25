namespace ShopAssistant.Infrastructure.TextProcessing.SemanticSearch.Embeddings;

using System.IO;

/// <summary>
/// Loads and stores language-specific float[] embeddings in memory.
/// </summary>
public class EmbeddingsStore
{
    public IReadOnlyList<float[]> Embeddings { get; }

    /// <summary>
    /// Loads embeddings from a language-specific binary file.
    /// </summary>
    public EmbeddingsStore(string embeddingsFilePath)
    {
        Embeddings = LoadEmbeddings(embeddingsFilePath);
    }

    private static List<float[]> LoadEmbeddings(string path)
    {
        var result = new List<float[]>();
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs);
        int count = br.ReadInt32();
        int dim = br.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var vec = new float[dim];
            for (int j = 0; j < dim; j++)
                vec[j] = br.ReadSingle();
            result.Add(vec);
        }
        return result;
    }
}