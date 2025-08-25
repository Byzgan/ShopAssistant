namespace ShopAssistant.Contracts.Models.Integrations;

/// <summary>
/// Model for requesting product recommendations.
/// </summary>
public class RecommendationRequest
{
    public string Category { get; set; } = string.Empty;
    public decimal? Budget { get; set; }
    public string Preference { get; set; } = string.Empty;
    public bool DiscountOnly { get; set; }
}