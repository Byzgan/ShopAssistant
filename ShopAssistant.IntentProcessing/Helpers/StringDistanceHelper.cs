namespace ShopAssistant.IntentProcessing.Helpers;

/// <summary>
/// Provides helper methods for calculating string distances.
/// </summary>
public static class StringDistanceHelper
{
    /// <summary>
    /// Calculates the Levenshtein distance between two strings.
    /// The Levenshtein distance is a measure of the minimum number of single-character edits
    /// (insertions, deletions, or substitutions) required to change one string into the other.
    /// </summary>
    /// <param name="s">The first string.</param>
    /// <param name="t">The second string.</param>
    /// <returns>The Levenshtein distance between the two strings.</returns>
    public static int LevenshteinDistance(string s, string t)
    {
        if (s == t) return 0;
        if (s.Length == 0) return t.Length;
        if (t.Length == 0) return s.Length;

        var d = new int[s.Length + 1, t.Length + 1];

        for (int i = 0; i <= s.Length; i++)
            d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++)
            d[0, j] = j;

        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[s.Length, t.Length];
    }
}