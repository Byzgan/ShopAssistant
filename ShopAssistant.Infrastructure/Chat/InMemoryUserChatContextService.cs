namespace ShopAssistant.Infrastructure.Chat;

using System.Collections.Concurrent;
using System.Threading.Tasks;
using ShopAssistant.Contracts.Interfaces.Chat;
using Contracts.Models.Intent;

/// <summary>
/// In-memory implementation of IUserChatContextService.
/// Stores chat history and follow-up intent context per user in memory.
/// Not persistent across service restarts. Thread-safe via ConcurrentDictionary.
/// </summary>
public class InMemoryUserChatContextService : IUserChatContextService
{
    // Store per-user chat history (FIFO, thread-safe)
    private readonly ConcurrentDictionary<string, List<string>> _userMessages = new();

    // Store per-user pending intent (follow-up) context
    private readonly ConcurrentDictionary<string, PendingIntentContext?> _pendingIntents = new();

    /// <summary>
    /// Retrieves the chat history for a user.
    /// </summary>
    public Task<List<string>> GetUserHistoryAsync(string uniqueUserId)
    {
        _userMessages.TryGetValue(uniqueUserId, out var history);
        return Task.FromResult(history ?? []);
    }

    /// <summary>
    /// Adds a message to the user's chat history.
    /// Maintains FIFO, e.g. for a maximum number of messages (optional).
    /// </summary>
    public Task AddUserMessageAsync(string uniqueUserId, string message)
    {
        var messages = _userMessages.GetOrAdd(uniqueUserId, _ => []);
        lock (messages)
        {
            messages.Add(message);
            // Optional: limit history length for each user, e.g. max 50
            if (messages.Count > 50)
                messages.RemoveAt(0);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears all chat history for the user.
    /// </summary>
    public Task ClearUserHistoryAsync(string uniqueUserId)
    {
        _userMessages.TryRemove(uniqueUserId, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the number of active users in the cache (for monitoring).
    /// </summary>
    public int GetActiveUsersCount() => _userMessages.Count;

    /// <summary>
    /// Cleans up inactive locks (dummy for in-memory; can be implemented for distributed locks).
    /// </summary>
    public void CleanupInactiveLocks()
    {
        // No-op for in-memory implementation.
    }

    /// <summary>
    /// Gets the pending follow-up context for a user, or null if none.
    /// </summary>
    public Task<PendingIntentContext?> GetPendingIntentAsync(string uniqueUserId)
    {
        _pendingIntents.TryGetValue(uniqueUserId, out var context);
        return Task.FromResult(context);
    }

    /// <summary>
    /// Sets (or clears) the pending follow-up context for a user. Pass null to clear.
    /// </summary>
    public Task SetPendingIntentAsync(string uniqueUserId, PendingIntentContext? context)
    {
        if (context == null)
            _pendingIntents.TryRemove(uniqueUserId, out _);
        else
            _pendingIntents[uniqueUserId] = context;

        return Task.CompletedTask;
    }
}
