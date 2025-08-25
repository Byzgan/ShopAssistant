using ShopAssistant.Contracts.Models.Intent;

namespace ShopAssistant.Contracts.Interfaces.Chat;

/// <summary>
/// Interface for managing per-user chat message history using Garnet/Redis or in-memory storage.
/// Provides methods for retrieving, adding, and clearing chat history for each user.
/// Also manages per-user pending follow-up context for multi-turn dialog.
/// </summary>
public interface IUserChatContextService
{
    /// <summary>
    /// Retrieves the chat history for a user.
    /// </summary>
    /// <param name="uniqueUserId">User unique identifier.</param>
    /// <returns>List of message strings (most recent last).</returns>
    Task<List<string>> GetUserHistoryAsync(string uniqueUserId);

    /// <summary>
    /// Adds a message to the user's chat history and maintains history size.
    /// </summary>
    /// <param name="uniqueUserId">User unique identifier.</param>
    /// <param name="message">Message text.</param>
    Task AddUserMessageAsync(string uniqueUserId, string message);

    /// <summary>
    /// Clears all chat history for the user.
    /// </summary>
    /// <param name="uniqueUserId">User unique identifier.</param>
    Task ClearUserHistoryAsync(string uniqueUserId);

    /// <summary>
    /// Gets the number of active users in the cache (for monitoring).
    /// </summary>
    int GetActiveUsersCount();

    /// <summary>
    /// Cleans up inactive locks (can be called periodically).
    /// </summary>
    void CleanupInactiveLocks();

    /// <summary>
    /// Gets the pending follow-up context for a user, or null if none.
    /// </summary>
    /// <param name="uniqueUserId">User unique identifier.</param>
    /// <returns>PendingIntentContext or null.</returns>
    Task<PendingIntentContext?> GetPendingIntentAsync(string uniqueUserId);

    /// <summary>
    /// Sets (or clears) the pending follow-up context for a user.
    /// Pass null to clear.
    /// </summary>
    /// <param name="uniqueUserId">User unique identifier.</param>
    /// <param name="context">PendingIntentContext or null.</param>
    Task SetPendingIntentAsync(string uniqueUserId, PendingIntentContext? context);
}
