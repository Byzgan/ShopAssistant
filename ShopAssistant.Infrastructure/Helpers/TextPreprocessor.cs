using System.Text.RegularExpressions;

namespace ShopAssistant.Infrastructure.Helpers;

/// <summary>
/// Provides consistent text preprocessing for embeddings and comparisons.
/// </summary>
public static class TextPreprocessor
{
    private static readonly Regex MultipleSpaces = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex Punctuation = new(@"[^\w\s]", RegexOptions.Compiled);

    /// <summary>
    /// Cleans input by trimming, lowercasing, removing punctuation, and collapsing whitespace.
    /// </summary>
    public static string Clean(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        input = input.Trim().ToLowerInvariant();
        input = Punctuation.Replace(input, "");
        input = MultipleSpaces.Replace(input, " ");
        return input;
    }

    /// <summary>
    /// Normalizes an embedding vector using L2 norm.
    /// </summary>
    public static float[] Normalize(float[] vector)
    {
        if (vector.Length == 0)
            return vector;

        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        if (norm == 0f)
            return vector;

        return vector.Select(v => v / norm).ToArray();
    }
}