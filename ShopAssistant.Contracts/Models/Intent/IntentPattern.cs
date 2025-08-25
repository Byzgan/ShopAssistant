namespace ShopAssistant.Contracts.Models.Intent;

using System.Text.Json.Serialization;
using Enums;

/// <summary>
/// Intent pattern description that is deserialized from JSON (by your cache/service) and
/// consumed at runtime by the intent matcher. This class performs no file I/O.
/// </summary>
public class IntentPattern
{
    /// <summary>
    /// Unique intent code (e.g., <see cref="Intent.OrderCancel"/>).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonPropertyName("intent")]
    public Intent Intent { get; set; }

    /// <summary>
    /// Example phrases for keyword/regex/fuzzy checks and for semantic (embedding) comparisons.
    /// Embeddings for these phrases are precomputed per-language by the embedding pipeline and
    /// provided to the matcher via caches/services (not read here).
    /// </summary>
    [JsonPropertyName("semanticPhrases")]
    public List<string>? SemanticPhrases { get; set; }

    /// <summary>
    /// Negative phrases/tokens. If the input contains any of these (after normalization),
    /// the pattern is rejected early (hard block).
    /// </summary>
    [JsonPropertyName("negativePhrases")]
    public List<string>? NegativePhrases { get; set; }

    /// <summary>
    /// Minimum cosine similarity required for semantic matching (0..1).
    /// If omitted, the matcher uses a conservative default.
    /// </summary>
    [JsonPropertyName("embeddingThreshold")]
    public float? EmbeddingThreshold { get; set; }

    /// <summary>
    /// Minimum fraction of token overlap required for the fuzzy (partial keyword) stage (0..1).
    /// If omitted, the matcher uses a conservative default (e.g., 0.60).
    /// </summary>
    [JsonPropertyName("partialKeywordCoverage")]
    public double? PartialKeywordCoverage { get; set; }

    /// <summary>
    /// Anchor tokens grouped by concept.
    /// Each inner list represents a synonym group (OR). All groups must be satisfied (AND).
    /// Example (cancel + order): [["cancel","stop","withdraw","reverse"], ["order","purchase"]]
    /// When null or empty, no anchor constraint is applied for this intent.
    /// </summary>
    [JsonPropertyName("requiredTokens")]
    public List<List<string>>? RequiredTokens { get; set; }

    /// <summary>
    /// Hard-block tokens/phrases. If any present, the pattern is rejected immediately.
    /// Complements <see cref="NegativePhrases"/> with a stricter semantics.
    /// </summary>
    [JsonPropertyName("forbiddenTokens")]
    public List<string>? ForbiddenTokens { get; set; }
}
