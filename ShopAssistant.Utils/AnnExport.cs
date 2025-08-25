using ShopAssistant.Contracts.Models.KnowledgeBase;

namespace ShopAssistant.Utils;

using System.Text.Json;

/// <summary>
/// Provides methods for exporting embeddings, meta-information, and ANN index bytes to disk.
/// </summary>
public static class AnnExport
{
    /// <summary>
    /// Exports embeddings, meta-information, and ANN index bytes to respective files.
    /// </summary>
    /// <param name="questions">List of embedded questions with vector data and metadata.</param>
    /// <param name="embeddingDimension">Vector size.</param>
    /// <param name="indexBytes">Serialized ANN index as byte array.</param>
    /// <param name="exportBasePath">Target directory for files.</param>
    /// <param name="languageCode">Language code.</param>
    public static void ExportAll(List<QuestionEmbedding> questions, int embeddingDimension, byte[] indexBytes, string exportBasePath, string languageCode)
    {
        if (questions == null || questions.Count == 0)
            throw new ArgumentException("Question list is null or empty.", nameof(questions));
        if (indexBytes == null || indexBytes.Length == 0)
            throw new ArgumentException("Index bytes are null or empty.", nameof(indexBytes));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("Language code must be provided.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(exportBasePath))
            throw new ArgumentException("Export base path must be provided.", nameof(exportBasePath));

        var filePrefix = languageCode.Trim().ToLowerInvariant();
        var kbEmbeddingsFile = Path.Combine(exportBasePath, $"kb_embeddings_{filePrefix}.bin");
        var kbMetaFile = Path.Combine(exportBasePath, $"kb_meta_{filePrefix}.json");
        var kbIndexFile = Path.Combine(exportBasePath, $"kb_index_{filePrefix}.hnsw");

        Directory.CreateDirectory(exportBasePath);

        // Write embeddings as binary
        using (var bw = new BinaryWriter(File.Open(kbEmbeddingsFile, FileMode.Create, FileAccess.Write, FileShare.None)))
        {
            bw.Write(questions.Count);
            bw.Write(embeddingDimension);
            foreach (var q in questions)
            {
                if (q.Embedding == null || q.Embedding.Length != embeddingDimension)
                    throw new InvalidOperationException($"Invalid embedding for question ID {q.Id}");

                foreach (var value in q.Embedding)
                    bw.Write(value);
            }
        }

        // Optional: Check for duplicate question IDs
        var duplicateIds = questions.GroupBy(q => q.Id).Where(g => g.Count() > 1).ToList();
        if (duplicateIds.Any())
        {
            Console.WriteLine($"Warning: Duplicate question IDs detected for language '{languageCode}':");
            foreach (var dup in duplicateIds)
                Console.WriteLine($" - ID {dup.Key}: {dup.Count()} occurrences");
        }

        // Write meta-information as JSON
        var metaList = questions.Select(q => new
        {
            q.Id,
            q.Question,
            q.KnowledgeId,
        }).ToList();

        File.WriteAllText(kbMetaFile, JsonSerializer.Serialize(metaList, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        // Write ANN index bytes
        File.WriteAllBytes(kbIndexFile, indexBytes);
    }
}
