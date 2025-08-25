namespace ShopAssistant.Contracts.Enums;

/// <summary>
/// Types of user intents recognized by the assistant.
/// </summary>
public enum Intent
{
    Unknown,
    Recommend,
    PaymentOptions,
    ProductSearch,
    OrderStatus,
    DiscountsAndPromotions,
    AccountManagement,
    DeliveryInfo,
    ChangeDeliveryAddress,
    ContactSupport,
    Refund,
    OrderCancel,
    FAQ
}