namespace ShopAssistant.Contracts.Interfaces.User;

using ShopAssistant.Contracts.Models.User;

/// <summary>
/// Provides access to the current authenticated user within the request scope.
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// Gets the current authenticated user, or null if unauthenticated.
    /// </summary>
    User? CurrentUser { get; }
}