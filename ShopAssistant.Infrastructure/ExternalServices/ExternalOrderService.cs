namespace ShopAssistant.Infrastructure.ExternalServices;

using Contracts.Interfaces.Integrations;
using ShopAssistant.Contracts.Models.User;

public class ExternalOrderService : IOrderService
{
    public async Task<string> GetLatestOrderStatusAsync(User? user, string language)
    {
        // Simulate external service call
        await Task.Delay(100);

        // Simulate result, real logic would query an API or DB
        return language.ToLowerInvariant() == "no" 
            ? "Sendt" 
            : "Shipped";
    }

    public async Task<string> GetOrderStatusAsync(User user, string orderNumber, string language)
    {
        // Simulate external service call
        await Task.Delay(100);

        // Simulate lookup by order number
        if (orderNumber == "12345")
            return language.ToLowerInvariant() == "no" 
                ? "Behandling" 
                : "Processing";

        return string.Empty;
    }

    public async Task<bool> ChangeDeliveryAddressAsync(User user, string orderNumber, string newAddress, string language)
    {
        // Simulate external service call
        await Task.Delay(100);
        
        // Dummy implementation: always return true
        return orderNumber != "12345";
    }

    /// <summary>
    /// Checks if the delivery address can be changed for a given order.
    /// </summary>
    /// <param name="user">Current user instance.</param>
    /// <param name="orderNumber">Order number.</param>
    /// <returns>True if the delivery address can be changed; otherwise, false.</returns>
    public async Task<bool> CanChangeDeliveryAddressAsync(User user, string orderNumber)
    {
        // Simulate external service call
        await Task.Delay(100);

        // Only allow address change for orders not equal to "12345"
        return orderNumber != "12345";
    }
}
