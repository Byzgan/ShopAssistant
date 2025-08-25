using ShopAssistant.Contracts.Models.Chat;

namespace ShopAssistant.Contracts.Interfaces.Chat;

/// <summary>
/// Chat service interface for processing user messages, including ambiguity handling.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Processes a user chat request, returns answer or ambiguity prompt if needed.
    /// </summary>
    Task<ChatResponse?> ProcessMessageAsync(ChatRequest request);
}