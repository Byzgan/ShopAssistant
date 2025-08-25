namespace ShopAssistant.Contracts.Interfaces.TextProcessing;
/// <summary>
/// General interface for a language stemmer.
/// </summary>
public interface IStemmer
{
    /// <summary>
    /// Stems the provided word to its base form.
    /// </summary>
    /// <param name="word">Input word (usually lowercase).</param>
    /// <returns>Stemmed word.</returns>
    string Stem(string word);
}
