using NUnit.Framework;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ShopAssistant.Infrastructure.KnowledgeBase;
using ShopAssistant.Contracts.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShopAssistant.Tests.Infrastructure.KnowledgeBase;

[TestFixture]
public class TopicRolePermissionProviderTests
{
    [Test]
    public async Task Cached_Results_Reused_Across_Calls()
    {
        // Arrange: prime the IMemoryCache with the permissions dictionary to avoid disk reads.
        var cache = new MemoryCache(new MemoryCacheOptions());
        var dict = new Dictionary<KnowledgeTopic, HashSet<UserRole>>
        {
            [KnowledgeTopic.Portal] = new HashSet<UserRole> { UserRole.Admin, UserRole.User },
            [KnowledgeTopic.Order] = new HashSet<UserRole> { UserRole.User }
        };
        cache.Set("KnowledgeBase.TopicRolePermissions", dict, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });

        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["PermissionsFilePath"] = "/dev/null" // won't be used because cache is primed
            })
            .Build();

        var sut = new TopicRolePermissionProvider(cache, cfg, NullLogger<TopicRolePermissionProvider>.Instance);

        // Act & Assert
        Assert.That(await sut.IsRoleAllowedForTopic(UserRole.Admin, KnowledgeTopic.Portal), Is.True);
        Assert.That(await sut.IsRoleAllowedForTopic(UserRole.Anonymous, KnowledgeTopic.Portal), Is.False);

        var allowedForUser = await sut.GetAllowedTopicsForRole(UserRole.User);
        Assert.That(allowedForUser, Is.Not.Null);
        Assert.That(allowedForUser.Contains(KnowledgeTopic.Portal), Is.True);
        Assert.That(allowedForUser.Contains(KnowledgeTopic.Order), Is.True);
    }
}