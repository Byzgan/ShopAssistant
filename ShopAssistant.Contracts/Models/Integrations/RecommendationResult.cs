namespace ShopAssistant.Contracts.Models.Integrations;

/// <summary>
/// Model representing a single recommended product.
/// </summary>
public class RecommendationResult
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; } 
    public string Url { get; set; } = string.Empty;
}