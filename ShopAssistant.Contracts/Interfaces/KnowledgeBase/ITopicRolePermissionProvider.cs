using ShopAssistant.Contracts.Enums;

namespace ShopAssistant.Contracts.Interfaces.KnowledgeBase;

/// <summary>
/// Exposes methods for checking which <see cref="UserRole"/> can access which <see cref="KnowledgeTopic"/>.
/// Used to enforce topic-based security restrictions in chat scenarios and knowledge base queries.
/// </summary>
public interface ITopicRolePermissionProvider
{
    /// <summary>
    /// Gets the set of topics accessible to the specified user role.
    /// </summary>
    /// <param name="role">The role to check (Admin, User, or Anonymous).</param>
    /// <returns>All <see cref="KnowledgeTopic"/> values accessible by the given role.</returns>
    Task<HashSet<KnowledgeTopic>> GetAllowedTopicsForRole(UserRole role);

    /// <summary>
    /// Checks whether a specific role is permitted to access a specific topic.
    /// </summary>
    /// <param name="role">The user role to check.</param>
    /// <param name="topic">The topic being accessed.</param>
    /// <returns>True if the role has access to the topic, false otherwise.</returns>
    Task<bool> IsRoleAllowedForTopic(UserRole role, KnowledgeTopic topic);

    /// <summary>
    /// Initializes the in-memory cache from disk. Should be called on application startup.
    /// </summary>
    Task InitializeCacheAsync();
}