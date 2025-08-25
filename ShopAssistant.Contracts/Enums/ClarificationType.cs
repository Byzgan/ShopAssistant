using System.Text.Json.Serialization;

namespace ShopAssistant.Contracts.Enums;

/// <summary>
/// Specifies the kind of ambiguity that requires user clarification in a chat scenario.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClarificationType
{
    None,
    Intent,
    Category,
}