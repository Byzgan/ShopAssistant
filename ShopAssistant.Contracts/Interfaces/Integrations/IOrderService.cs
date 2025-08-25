namespace ShopAssistant.Contracts.Interfaces.Integrations;

using Models.User;

/// <summary>
/// Service for querying and updating order information.
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// Gets the order status for the specified user and order number.
    /// </summary>
    /// <param name="user">Current user instance.</param>
    /// <param name="orderNumber">Order number.</param>
    /// <param name="language">Language code (e.g., "en", 'no').</param>
    /// <returns>Order status as a string.</returns>
    Task<string> GetOrderStatusAsync(User user, string orderNumber, string language);

    /// <summary>
    /// Gets the latest order status for the specified user.
    /// </summary>
    /// <param name="user">Current user instance.</param>
    /// <param name="language">Language code (e.g., "en", 'no').</param>
    /// <returns>Order status of the latest order as a string.</returns>
    Task<string> GetLatestOrderStatusAsync(User user, string language);

    /// <summary>
    /// Changes the delivery address for the specified order.
    /// </summary>
    /// <param name="user">Current user instance.</param>
    /// <param name="orderNumber">Order number.</param>
    /// <param name="newAddress">New delivery address.</param>
    /// <param name="language">Language code (e.g., "en", 'no').</param>
    /// <returns>True if the address was changed successfully, otherwise false.</returns>
    Task<bool> ChangeDeliveryAddressAsync(User user, string orderNumber, string newAddress, string language);


    /// <summary>
    /// Checks if the delivery address can be changed for a given order.
    /// </summary>
    /// <param name="user">Current user instance.</param>
    /// <param name="orderNumber">Order number.</param>
    /// <returns>True if the delivery address can be changed; otherwise, false.</returns>
    Task<bool> CanChangeDeliveryAddressAsync(User user, string orderNumber);
}