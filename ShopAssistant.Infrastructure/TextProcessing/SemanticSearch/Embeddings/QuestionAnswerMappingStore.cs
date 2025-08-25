namespace ShopAssistant.Infrastructure.TextProcessing.SemanticSearch.Embeddings;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// In-memory, read-only store that maps QuestionId → KnowledgeId and QuestionId → QuestionText
/// for a single language. The JSON is read once at startup; thereafter the instance is served
/// from <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/> via EmbeddingIndexCacheService.
/// </summary>
public class QuestionAnswerMappingStore
{
    /// <summary>
    /// Mapping between question variant identifiers and knowledge item identifiers.
    /// </summary>
    public IReadOnlyDictionary<int, int> QuestionAnswerMapping { get; }

    /// <summary>
    /// Canonical question text per question variant identifier.
    /// </summary>
    public IReadOnlyDictionary<int, string> QuestionTexts { get; }

    /// <summary>
    /// Loads and materializes the mapping and texts from a JSON file.
    /// This constructor performs file IO and JSON parsing once; the caller is expected to
    /// cache the resulting instance (see EmbeddingIndexCacheService).
    /// </summary>
    /// <param name="metadataJsonPath">Absolute or relative path to the per-language metadata JSON.</param>
    /// <exception cref="ArgumentException">If the path is null/empty.</exception>
    /// <exception cref="FileNotFoundException">If the file does not exist.</exception>
    /// <exception cref="InvalidOperationException">If parsing fails or no entries are loaded.</exception>
    public QuestionAnswerMappingStore(string metadataJsonPath)
    {
        if (string.IsNullOrWhiteSpace(metadataJsonPath))
            throw new ArgumentException("Metadata JSON path must not be null or empty.", nameof(metadataJsonPath));

        if (!File.Exists(metadataJsonPath))
            throw new FileNotFoundException($"Metadata JSON file not found at path: {metadataJsonPath}", metadataJsonPath);

        var items = LoadMetadata(metadataJsonPath);
        if (items.Count == 0)
            throw new InvalidOperationException("No question-answer entries were loaded from the provided metadata JSON.");

        // Build immutable views
        QuestionAnswerMapping = items.ToDictionary(m => m.Id, m => m.KnowledgeId);
        QuestionTexts = items.ToDictionary(m => m.Id, m => m.QuestionText ?? string.Empty);
    }

    /// <summary>
    /// Parses the JSON file into a normalized list of <see cref="QuestionMetadata"/>.
    /// Performs basic validation and provides backward compatibility for missing fields.
    /// </summary>
    private static List<QuestionMetadata> LoadMetadata(string jsonPath)
    {
        string json = File.ReadAllText(jsonPath);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var raw = JsonSerializer.Deserialize<List<QuestionMetadata>>(json, SerializerOptions) ?? [];
            var result = new List<QuestionMetadata>(raw.Count);
            var seen = new HashSet<int>();

            foreach (var item in raw)
            {
                Validate(item);

                if (!seen.Add(item.Id))
                    throw new InvalidOperationException($"Duplicate question ID found in metadata: {item.Id}");

                // Normalize: allow missing questionText for backward compatibility
                var normalized = item with { QuestionText = item.QuestionText?.Trim() ?? string.Empty };
                result.Add(normalized);
            }

            return result;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to deserialize metadata JSON.", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("An error occurred while loading the question-answer metadata.", ex);
        }
    }

    /// <summary>
    /// Basic validation for required fields.
    /// </summary>
    private static void Validate(QuestionMetadata item)
    {
        if (item.Id < 0)
            throw new InvalidOperationException($"Invalid question ID found in metadata: {item.Id}");

        if (item.KnowledgeId <= 0)
            throw new InvalidOperationException($"Invalid knowledge ID found in metadata: {item.KnowledgeId}");
    }


    /// <summary>
    /// JSON contract for a single row in kb_meta_{lang}.json.
    /// </summary>
    private readonly record struct QuestionMetadata(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("knowledgeId")] int KnowledgeId,
        [property: JsonPropertyName("question")] string? QuestionText
    );

    /// <summary>
    /// System.Text.Json options: case-insensitive names and tolerant number parsing.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
}
