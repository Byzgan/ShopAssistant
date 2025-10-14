namespace ShopAssistant.Contracts.Config;

public class EmbeddingsOptions
{
    public required string ModelPath { get; set; }
    public required string VocabPath { get; set; }
    public required int VectorSize { get; set; }
}
