namespace ShopAssistant.Contracts.Models.KnowledgeBase;

/// <summary>
/// Represents an embedded question entry used for ANN export.
/// </summary>
public class QuestionEmbedding
{
    /// <summary>
    /// Zero-based index assigned to the question (used in HNSW index).
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Preprocessed question text used to generate the embedding.
    /// </summary>
    public required string Question { get; init; } 

    /// <summary>
    /// Embedding vector generated for the question.
    /// </summary>
    public float[] Embedding { get; init; } = [];

    /// <summary>
    /// Identifier of the knowledge base item this question belongs to.
    /// </summary>
    public int KnowledgeId { get; init; }

    /// <summary>
    /// Language code (e.g., \"en\", \"no\").  
    /// Not required if you only export one language per file.
    /// </summary>
    public required string Language { get; init; }
}