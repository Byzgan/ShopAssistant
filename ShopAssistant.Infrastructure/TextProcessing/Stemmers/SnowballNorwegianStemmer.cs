using ShopAssistant.Contracts.Interfaces.TextProcessing;

namespace ShopAssistant.Infrastructure.TextProcessing.Stemmers;

/// <summary>
/// Norwegian Bokmål stemmer inspired by Snowball algorithm.
/// Implements region finding (R1), multi-step suffix stripping,
/// and handling of double consonants.
/// Covers the majority of practical cases for Norwegian stemming.
/// </summary>
public class SnowballNorwegianStemmer : IStemmer
{
    // Main Norwegian endings (see Snowball algorithm)
    private static readonly string[] Step1Suffixes = {
        "hetene", "heter", "heten", "endes", "ande", "ende", "enes", "ane", "ene",
        "ens", "ers", "ets", "ens", "ers", "ets", "ers", "ets",
        "er", "en", "et", "ar", "er", "as", "es", "ed", "a", "e", "s"
    };

    private static readonly string[] Step2Suffixes = {
        "erte", "est", "eleg", "elig", "el", "lig", "ig"
    };

    // Norwegian vowels
    private static readonly char[] Vowels = { 'a', 'e', 'i', 'o', 'u', 'y', 'å', 'ø', 'æ' };

    /// <summary>
    /// Stem a Norwegian word using a simplified Snowball algorithm.
    /// </summary>
    /// <param name="word">Input word (lowercase expected).</param>
    /// <returns>Stemmed word.</returns>
    public string Stem(string word)
    {
        if (string.IsNullOrWhiteSpace(word) || word.Length < 3)
            return word;

        word = word.ToLowerInvariant();

        // 1. Find R1 region (first non-vowel after a vowel, or end of word)
        int r1 = FindR1(word);

        // 2. Step 1: Remove the longest matching suffix from R1
        string stem = RemoveLongestSuffix(word, r1, Step1Suffixes);

        // 3. Step 2: Remove common derivational endings (from R1)
        stem = RemoveLongestSuffix(stem, r1, Step2Suffixes);

        // 4. Remove last "s" if it's not part of a double consonant or after a vowel
        if (stem.Length > 3 && stem.EndsWith("s"))
        {
            char beforeS = stem[stem.Length - 2];
            if (!"s".Contains(beforeS) && !IsVowel(beforeS))
                stem = stem.Substring(0, stem.Length - 1);
        }

        // 5. Remove double consonant at end (e.g., -nn, -tt, -ll)
        if (stem.Length > 3 &&
            stem[stem.Length - 1] == stem[stem.Length - 2] &&
            !IsVowel(stem[stem.Length - 1]))
        {
            stem = stem.Substring(0, stem.Length - 1);
        }

        return stem;
    }

    /// <summary>
    /// Find the start index of R1 region (as per Snowball definition).
    /// </summary>
    private static int FindR1(string word)
    {
        for (int i = 1; i < word.Length; i++)
        {
            if (IsVowel(word[i - 1]) && !IsVowel(word[i]))
                return i + 1;
        }
        return word.Length;
    }

    /// <summary>
    /// Remove the longest matching suffix (from suffixes list) that is in R1 region.
    /// </summary>
    private static string RemoveLongestSuffix(string word, int r1, string[] suffixes)
    {
        foreach (var suffix in suffixes)
        {
            if (word.EndsWith(suffix) && word.Length - suffix.Length >= r1)
            {
                return word.Substring(0, word.Length - suffix.Length);
            }
        }
        return word;
    }

    /// <summary>
    /// Checks if a character is a Norwegian vowel.
    /// </summary>
    private static bool IsVowel(char c)
    {
        foreach (var v in Vowels)
            if (c == v)
                return true;
        return false;
    }
}