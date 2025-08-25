namespace ShopAssistant.Contracts.Models.Validations;

using Intent;
using KnowledgeBase;
using Enums;

/// <summary>
/// Immutable DTO representing a detected conflict between a KB question and an intent pattern.
/// Value-based equality, best for reporting and logging.
/// </summary>
public record ConflictResult(
    string Language,
    KnowledgeItem KnowledgeItem,
    IntentPattern IntentPattern,
    string Question,
    MatchType MatchType,
    float Score,
    string? MatchedPhrase);