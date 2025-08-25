using ShopAssistant.Contracts.Enums;
using ShopAssistant.Contracts.Interfaces.KnowledgeBase;
using ShopAssistant.Contracts.Interfaces.TextProcessing;
using ShopAssistant.Contracts.Models.Chat;

namespace ShopAssistant.Infrastructure.KnowledgeBase;

/// <summary>
/// Provides semantic search and topic filtering for user questions.
/// </summary>
public class KnowledgeBaseQueryService(ISemanticSearchService semanticSearchService, ITextEmbedder embedder) : IKnowledgeBaseQueryService
{
    /// <inheritdoc/>
    public async Task<SearchResult?> FindAnswerAsync(string question, string language, HashSet<KnowledgeTopic> allowedTopics)
    {
        var embedding = await embedder.GetEmbeddingAsync(question);
        var searchResult = await semanticSearchService.GetBestSemanticMatchAsync(embedding, language, allowedTopics);

        return searchResult;
    }
}