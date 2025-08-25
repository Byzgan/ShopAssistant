namespace ShopAssistant.Contracts.Models.Integrations;

/// <summary>
/// Represents a structured request for product search.
/// </summary>
public class ProductSearchRequest
{
    /// <summary>
    /// Gets or sets the product type or category (e.g., "headphones").
    /// </summary>
    public string? ProductType { get; set; }

    /// <summary>
    /// Gets or sets the preferred brand, if specified.
    /// </summary>
    public string? Brand { get; set; }
}