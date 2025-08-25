namespace ShopAssistant.Contracts.Models.Integrations;

/// <summary>
/// Represents a product returned from an external product search.
/// </summary>
public class ProductSearchResult
{
    /// <summary>
    /// Gets or sets the product name or title.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the product price (as text for demo purposes).
    /// </summary>
    public string Price { get; set; } = null!;

    /// <summary>
    /// Gets or sets the URL to the product details or page.
    /// </summary>
    public string Url { get; set; } = null!;
}