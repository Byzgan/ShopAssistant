namespace ShopAssistant.Infrastructure.User;

using Microsoft.AspNetCore.Http;
using ShopAssistant.Contracts.Interfaces.User;
using ShopAssistant.Contracts.Models.User;


/// <summary>
/// Provides access to the current authenticated user from the HTTP context.
/// The user is expected to be set in <see cref="HttpContext.Items"/> by middleware (e.g., UserContextMiddleware).
/// </summary>
public class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    /// <summary>
    /// Gets the current user from the HTTP context.
    /// </summary>
    public User? CurrentUser => httpContextAccessor.HttpContext?.Items["User"] as User;
}