using ShopAssistant.Contracts.Models.Integrations;

namespace ShopAssistant.Contracts.Interfaces.Integrations;

/// <summary>
/// Interface for product recommendation service.
/// </summary>
public interface IRecommendationService
{
    /// <summary>
    /// Gets recommended products based on user's preferences.
    /// </summary>
    /// <param name="request">Recommendation query with all user criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of recommendations.</returns>
    Task<List<RecommendationResult>> GetRecommendationsAsync(RecommendationRequest request, CancellationToken cancellationToken);

    Task<Dictionary<string, List<string>>> GetKnownCategoriesAsync(string language);
}