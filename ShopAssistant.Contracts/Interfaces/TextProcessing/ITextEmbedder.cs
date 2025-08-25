namespace ShopAssistant.Contracts.Interfaces.TextProcessing;

/// <summary>
/// Defines a contract for text embedding generation using LLM models.
/// Used to transform text (questions, knowledge base items, etc.) into numeric vector representations.
/// </summary>
public interface ITextEmbedder
{
    /// <summary>
    /// Generates a vector embedding for the provided text in the specified language.
    /// </summary>
    /// <param name="text">The input text to be embedded.</param>
    /// <returns>A float array representing the vector embedding of the input text.</returns>
    Task<float[]> GetEmbeddingAsync(string text);
}