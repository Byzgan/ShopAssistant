using ShopAssistant.Contracts.Models.KnowledgeBase;

namespace ShopAssistant.Contracts.Interfaces.KnowledgeBase;

/// <summary>
/// Abstraction for loading all knowledge items from a specific source, such as JSON files, a database, or an API.
/// </summary>
public interface IKnowledgeLoader
{
    /// <summary>
    /// Loads all knowledge items including their associated questions.
    /// </summary>
    /// <returns>List of knowledge items.</returns>
    Task<List<KnowledgeItem>> LoadAllAsync();
}