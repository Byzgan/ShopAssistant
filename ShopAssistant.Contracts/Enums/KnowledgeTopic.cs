namespace ShopAssistant.Contracts.Enums;

/// <summary>
/// Represents all knowledge topics existing in the system.
/// This is used for permission checks, intent matching, and topic classification.
/// </summary>
public enum KnowledgeTopic
{
    Portal,
    Order,
    Shipping,
    Returns,
    Warranty,
    Account,
    Support,
    ProductAvailability,
    DiscountsAndPromotions,
    OrderTracking,
    OrderCancellation,
    ChangingOrders,
    GiftCards,
    StoreLocations,
    ProductInformation
}
