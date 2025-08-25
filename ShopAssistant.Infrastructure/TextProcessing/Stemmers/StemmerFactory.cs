using ShopAssistant.Contracts.Interfaces.TextProcessing;

namespace ShopAssistant.Infrastructure.TextProcessing.Stemmers;

/// <summary>
/// Factory for language-specific stemmers.
/// </summary>
public static class StemmerFactory
{
    public static IStemmer GetStemmer(string lang)
    {
        return lang.ToLowerInvariant() switch
        {
            "no" => new SnowballNorwegianStemmer(),
            _ => new PorterEnglishStemmer()
        };
    }
}