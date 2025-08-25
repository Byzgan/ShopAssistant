namespace ShopAssistant.Contracts.Enums;

/// <summary>
/// Identifies which retriever produced a score/rank, or whether a result is fused.
/// </summary>
public enum RetrievalSource
{
    Unknown = 0,
    Bm25 = 1,
    Vector = 2,
    Hybrid = 3
}