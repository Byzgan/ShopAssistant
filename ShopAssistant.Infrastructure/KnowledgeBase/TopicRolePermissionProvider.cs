// ReSharper disable CollectionNeverUpdated.Local
// ReSharper disable ClassNeverInstantiated.Local
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShopAssistant.Contracts.Enums;
using ShopAssistant.Contracts.Interfaces.KnowledgeBase;
using ShopAssistant.Contracts.Models.KnowledgeBase;

namespace ShopAssistant.Infrastructure.KnowledgeBase;

/// <summary>
/// Loads and caches topic/role permissions from a JSON file. Must be explicitly initialized on application startup.
/// All business access is in-memory only.
/// </summary>
public class TopicRolePermissionProvider(IMemoryCache cache, IConfiguration configuration, ILogger<TopicRolePermissionProvider> logger) : ITopicRolePermissionProvider
{
    private const string PermissionsCacheKey = "KnowledgeBase.TopicRolePermissions";
    private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly string _permissionsFilePath = configuration["PermissionsFilePath"] ?? throw new InvalidOperationException("PermissionsFilePath setting is missing in configuration.");
    private readonly ILogger<TopicRolePermissionProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Asynchronously initializes (or refreshes) the in-memory cache from disk. Should be called on application startup.
    /// Throws on error so startup fails fast if permissions are missing or malformed.
    /// </summary>
    public async Task InitializeCacheAsync()
    {
        if (!File.Exists(_permissionsFilePath))
            throw new FileNotFoundException($"Permissions file not found: {_permissionsFilePath}");

        string json;
        try
        {
            await using var stream = File.OpenRead(_permissionsFilePath);
            json = await new StreamReader(stream).ReadToEndAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to read topic-role permissions file: {_permissionsFilePath}", ex);
        }

        List<TopicRolePermission>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<TopicRolePermission>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Failed to parse topic-role permissions JSON.", ex);
        }

        if (entries is null)
            throw new InvalidOperationException("Failed to parse topic-role permissions JSON.");

        var dict = new Dictionary<KnowledgeTopic, HashSet<UserRole>>();

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Topic))
            {
                _logger.LogWarning("Permission entry found with empty topic name. Entry will be skipped.");
                continue;
            }

            if (!Enum.TryParse<KnowledgeTopic>(entry.Topic, ignoreCase: true, out var topicEnum))
            {
                _logger.LogWarning("Unknown topic '{Topic}' in topic permissions config. Entry will be skipped.", entry.Topic);
                continue;
            }

            var roles = new HashSet<UserRole>();
            foreach (var roleStr in entry.AllowedRoles)
            {
                if (Enum.TryParse<UserRole>(roleStr, ignoreCase: true, out var roleEnum))
                {
                    roles.Add(roleEnum);
                }
                else
                {
                    _logger.LogWarning("Unknown user role '{Role}' for topic '{Topic}' in topic permissions config. Role will be skipped.", roleStr, entry.Topic);
                }
            }

            dict[topicEnum] = roles;
        }

        _cache.Set(PermissionsCacheKey, dict, new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.NeverRemove
        });

        _logger.LogInformation("Topic-role permissions cache successfully initialized with {Count} topics.", dict.Count);
    }

    /// <inheritdoc/>
    public async Task<HashSet<KnowledgeTopic>> GetAllowedTopicsForRole(UserRole role)
    {
        var permissions = await TryGetCachedPermissions();
        if (permissions is null || permissions.Count == 0)
            return [];

        var allowedTopics = new HashSet<KnowledgeTopic>();
        foreach (var (topic, roles) in permissions)
        {
            if (roles.Contains(role))
                allowedTopics.Add(topic);
        }

        return allowedTopics;
    }

    /// <inheritdoc/>
    public async Task<bool> IsRoleAllowedForTopic(UserRole role, KnowledgeTopic topic)
    {
        var permissions = await TryGetCachedPermissions();
        return permissions != null && permissions.TryGetValue(topic, out var allowedRoles) && allowedRoles.Contains(role);
    }

    /// <summary>
    /// Reads cached permissions. Initializes the cache if it has not already been initialized.
    /// </summary>
    private async Task<Dictionary<KnowledgeTopic, HashSet<UserRole>>?> TryGetCachedPermissions()
    {
        if (!_cache.TryGetValue(PermissionsCacheKey, out Dictionary<KnowledgeTopic, HashSet<UserRole>>? cached) || cached is null || cached.Count == 0)
        {
            await InitializeCacheAsync();
        }

        return cached;
    }
}
