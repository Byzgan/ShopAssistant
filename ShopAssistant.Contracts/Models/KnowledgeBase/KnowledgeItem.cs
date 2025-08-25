using System.Text.Json.Serialization;
using ShopAssistant.Contracts.Enums;

namespace ShopAssistant.Contracts.Models.KnowledgeBase;

/// <summary>
/// Represents a single knowledge base answer (FAQ entry).
/// Stores main metadata: topic, language, and answer text.
/// </summary>
public class KnowledgeItem
{
    /// <summary>
    /// Unique primary key. Autoincremented in the SQLite database.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Logical topic or category for grouping knowledge entries.
    /// Example: "Shipping", "Returns", "Ordering".
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public KnowledgeTopic Topic { get; set; }

    /// <summary>
    /// Two-letter ISO language code (e.g. "en", "no").
    /// Used for filtering and multilingual support.
    /// </summary>
    public required string Language { get; set; }

    /// <summary>
    /// Canonical answer text for all question variants in this knowledge item.
    /// </summary>
    public required string Answer { get; set; }

    /// <summary>
    /// List of related question variants.
    /// </summary>
    public required List<string> Questions { get; set; }
}