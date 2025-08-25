namespace ShopAssistant.Contracts.Interfaces.Integrations;

/// <summary>
/// Service for querying discount and promotion info.
/// </summary>
public interface IPromotionService
{
    Task<string> GetCurrentPromotionsAsync();
}