namespace ShopAssistant.Contracts.Models.Intent;

using Enums;

/// <summary>
/// Alternative intent result for ambiguity handling.
/// </summary>
public class IntentScore
{
    public Intent Intent { get; set; }
    public float Score { get; set; }
    public MatchType MatchType { get; set; } = MatchType.None;
}