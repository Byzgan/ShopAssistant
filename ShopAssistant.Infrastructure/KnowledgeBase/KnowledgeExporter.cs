namespace ShopAssistant.Infrastructure.KnowledgeBase;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShopAssistant.Contracts.Interfaces.TextProcessing;
using ShopAssistant.Contracts.Models.KnowledgeBase;
using Helpers;
using TextProcessing.SemanticSearch.Embeddings;
using Utils;

public class KnowledgeExporter(IConfiguration config, ITextEmbedder embedder, ILogger<KnowledgeExporter> logger)
{
    /// <summary>
    /// Exports per-language vector indexes and mapping with deterministic per-language QuestionIds.
    /// Guarantees: ANN record.Id == exported per-language QuestionId.
    /// </summary>
    public async Task ExportAsync()
    {
        var vectorSize = config.GetValue<int>("LLM:VectorSize");
        if (vectorSize <= 0)
            throw new InvalidOperationException("LLM:VectorSize must be set and positive in appsettings.json");

        var exportFolder = config.GetValue<string>("EmbeddingsPath")
            ?? throw new InvalidOperationException("Missing folder for ANN embeddings (EmbeddingsPath).");

        var exportBasePath = Path.Combine(Directory.GetCurrentDirectory(), "..", exportFolder);
        logger.LogInformation("Exporting ANN data to: {ExportBasePath}", exportBasePath);
        Directory.CreateDirectory(exportBasePath);

        // 1) Load KB items
        var knowledgeItems = await KnowledgeFileLoader.ReadAllKnowledgeItemsFromJsonAsync().ConfigureAwait(false);
        if (knowledgeItems.Count == 0)
        {
            logger.LogWarning("No knowledge items found for export.");
            return;
        }

        // 2) Prepare a deterministic, per-language list of rows WITHOUT de-duplication.
        //    Order inside each language: KnowledgeId asc, then the original question index within that item (0,1,2,...).
        var byLang = knowledgeItems
            .Where(ki => ki.Questions is { Count: > 0 })
            .GroupBy(ki => ki.Language) // language used as-is
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderBy(ki => ki.Id)
                    .SelectMany(ki => ki.Questions
                        .Select((q, idx) => new
                        {
                            Language = g.Key,
                            KnowledgeId = ki.Id,
                            Question = TextPreprocessor.Clean(q),
                            Seq = idx  // original position inside the item's Questions list
                        }))
                    .OrderBy(r => r.KnowledgeId)
                    .ThenBy(r => r.Seq)
                    .ToList()
            );

        if (byLang.Count == 0)
        {
            logger.LogWarning("No valid questions found to export.");
            return;
        }

        // 3) For each language, assign per-language QIDs, embed in that order, build ANN, export mapping.
        var maxParallel = Math.Min(Environment.ProcessorCount, 8);

        foreach (var (languageCode, rows) in byLang)
        {
            logger.LogInformation("Exporting language '{Lang}' with {Count} questions.", languageCode, rows.Count);

            // Fixed-size array: index == per-language QuestionId (qid)
            var perLang = new QuestionEmbedding[rows.Count];

            // Generate embeddings with bounded parallelism; write directly into perLang[qid].
            var opts = new ParallelOptions { MaxDegreeOfParallelism = maxParallel };
            await Parallel.ForEachAsync(Enumerable.Range(0, rows.Count), opts,
                async (qid, _) =>
                {
                    var row = rows[qid];
                    var emb = await embedder.GetEmbeddingAsync(row.Question).ConfigureAwait(false);

                    perLang[qid] = new QuestionEmbedding
                    {
                        Id = qid,
                        Question = row.Question,
                        Language = row.Language,
                        KnowledgeId = row.KnowledgeId,
                        Embedding = emb
                    };
                });

            // Sanity: ensure contiguous QIDs and filled embeddings
            for (int i = 0; i < perLang.Length; i++)
            {
                if (perLang[i] is null || perLang[i].Id != i || perLang[i].Embedding is null)
                    throw new InvalidOperationException($"Missing or non-contiguous QID at index {i} for language '{languageCode}'.");
            }

            // Build ANN from embeddings in QID order
            var embeddings = perLang.Select(e => e.Embedding).ToList();
            var annIndexPath = Path.Combine(exportBasePath, $"kb_index_{languageCode}.hnsw");

            var vectorStore = new HnswVectorStore(embeddings, vectorSize);
            vectorStore.SaveToFile(annIndexPath);
            var indexBytes = vectorStore.GetSerializedIndex();

            // Export mapping & metadata from the same ordered list (QID -> KID)
            // AnnExport.ExportAll must write mapping using e.Id (QID) and e.KnowledgeId (KID).
            AnnExport.ExportAll(perLang.ToList(), vectorSize, indexBytes, exportBasePath, languageCode);

            logger.LogInformation("Exported '{Lang}': QIDs 0..{MaxQid}, file={File}",
                languageCode, perLang.Length - 1, annIndexPath);
        }

        logger.LogInformation("Export complete.");
    }
}
