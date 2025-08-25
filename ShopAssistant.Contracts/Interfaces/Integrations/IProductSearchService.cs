using ShopAssistant.Contracts.Models.Integrations;

namespace ShopAssistant.Contracts.Interfaces.Integrations;

/// <summary>
/// Defines a contract for an external service to search for products based on a structured request.
/// </summary>
public interface IProductSearchService
{
    /// <summary>
    /// Searches for products matching the specified criteria.
    /// </summary>
    /// <param name="request">The structured product search request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching products (simulated for now).</returns>
    Task<IReadOnlyList<ProductSearchResult>> SearchProductsAsync(ProductSearchRequest request, CancellationToken cancellationToken);
}