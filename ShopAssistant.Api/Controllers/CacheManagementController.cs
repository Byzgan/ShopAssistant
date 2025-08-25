namespace ShopAssistant.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Contracts.Interfaces.Intent;
using Contracts.Interfaces.KnowledgeBase;
using Contracts.Interfaces.Localization;
using Infrastructure.KnowledgeBase;
using Infrastructure.TextProcessing.SemanticSearch.Embeddings;
using IntentProcessing.IntentDetectors;


/// <summary>
/// Administrative endpoints for refreshing in-memory application caches.
/// </summary>
[ApiController]
[Route("api/admin/cache")]
[Authorize(Roles = "Admin")] // Ensure only admins can access!
public class CacheManagementController(
    KnowledgeCacheInitializer knowledgeCacheInitializer,
    EmbeddingIndexCacheService embeddingCacheService,
    IIntentPatternCacheService patternCacheService,
    IntentEmbeddingCacheInitializer intentEmbeddingsInitializer,
    ITopicRolePermissionProvider topicRolePermissionProvider,
    ILocalizationService localizationService,
    KnowledgeLexicalIndexInitializer kbLexicalInitializer) : ControllerBase
{
    /// <summary>
    /// Refreshes all major application caches (knowledge, embeddings, intent, permissions, localization).
    /// Equivalent to backend startup logic.
    /// </summary>
    [HttpPost("refresh-all")]
    public async Task<IActionResult> RefreshAllCaches()
    {
        await embeddingCacheService.InitializeCacheAsync();
        await knowledgeCacheInitializer.InitializeCacheAsync();
        await patternCacheService.InitializeCacheAsync();
        await intentEmbeddingsInitializer.InitializeCacheAsync();
        await topicRolePermissionProvider.InitializeCacheAsync();
        await localizationService.InitializeCacheAsync();
        await kbLexicalInitializer.InitializeAsync();

        return Ok(new { message = "All caches refreshed successfully." });
    }

    /// <summary>
    /// Refreshes only the knowledge base cache.
    /// </summary>
    [HttpPost("refresh/knowledge")]
    public async Task<IActionResult> RefreshKnowledgeCache()
    {
        await knowledgeCacheInitializer.InitializeCacheAsync();
        return Ok(new { message = "Knowledge cache refreshed." });
    }

    /// <summary>
    /// Refreshes only the embeddings (vector store) cache.
    /// </summary>
    [HttpPost("refresh/embeddings")]
    public async Task<IActionResult> RefreshEmbeddingsCache()
    {
        await embeddingCacheService.InitializeCacheAsync();
        return Ok(new { message = "Embeddings cache refreshed." });
    }

    /// <summary>
    /// Refreshes only the BM25 index cache.
    /// </summary>
    [HttpPost("refresh/bm25Index")]
    public async Task<IActionResult> RefreshBm25IndexCache()
    {
        await kbLexicalInitializer.InitializeAsync();
        return Ok(new { message = "BM25 index cache refreshed." });
    }

    /// <summary>
    /// Refreshes only the intent pattern cache.
    /// </summary>
    [HttpPost("refresh/intent-patterns")]
    public async Task<IActionResult> RefreshIntentPatternsCache()
    {
        await patternCacheService.InitializeCacheAsync();
        return Ok(new { message = "Intent patterns cache refreshed." });
    }

    /// <summary>
    /// Refreshes only the intent embeddings cache.
    /// </summary>
    [HttpPost("refresh/intent-embeddings")]
    public async Task<IActionResult> RefreshIntentEmbeddingsCache()
    {
        await intentEmbeddingsInitializer.InitializeCacheAsync();
        return Ok(new { message = "Intent embeddings cache refreshed." });
    }

    /// <summary>
    /// Refreshes only the topic-role permissions cache.
    /// </summary>
    [HttpPost("refresh/permissions")]
    public async Task<IActionResult> RefreshPermissionsCache()
    {
        await topicRolePermissionProvider.InitializeCacheAsync();
        return Ok(new { message = "Role permissions cache refreshed." });
    }

    /// <summary>
    /// Refreshes only the localization messages cache.
    /// </summary>
    [HttpPost("refresh/localization")]
    public async Task<IActionResult> RefreshLocalizationCache()
    {
        await localizationService.InitializeCacheAsync();
        return Ok(new { message = "Localization cache refreshed." });
    }
}
