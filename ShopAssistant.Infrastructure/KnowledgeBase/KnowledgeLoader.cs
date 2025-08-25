namespace ShopAssistant.Infrastructure.KnowledgeBase;

using Microsoft.Extensions.Logging;
using Contracts.Interfaces.KnowledgeBase;
using Contracts.Models.KnowledgeBase;
using Utils;

/// <summary>
/// Concrete implementation of <see cref="IKnowledgeLoader"/> that loads knowledge items from JSON files using <see cref="KnowledgeFileLoader"/>.
/// </summary>
public class KnowledgeLoader : IKnowledgeLoader
{
    private readonly ILogger<KnowledgeLoader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeLoader"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostics and telemetry.</param>
    public KnowledgeLoader(ILogger<KnowledgeLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads all knowledge items from localization JSON files.
    /// This method delegates to <see cref="KnowledgeFileLoader.ReadAllKnowledgeItemsFromJsonAsync"/>.
    /// </summary>
    /// <returns>A list of fully populated <see cref="KnowledgeItem"/> objects.</returns>
    public async Task<List<KnowledgeItem>> LoadAllAsync()
    {
        _logger.LogInformation("Loading knowledge items from JSON...");

        var items = await KnowledgeFileLoader.ReadAllKnowledgeItemsFromJsonAsync();

        _logger.LogInformation("Loaded {Count} knowledge items.", items.Count);

        return items;
    }
}