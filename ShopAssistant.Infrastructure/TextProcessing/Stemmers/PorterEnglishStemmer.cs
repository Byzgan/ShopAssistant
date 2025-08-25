namespace ShopAssistant.Infrastructure.TextProcessing.Stemmers;

using ShopAssistant.Contracts.Interfaces.TextProcessing;
using System.Text;

/// <summary>
/// Production-grade implementation of the Porter Stemming Algorithm for English.
/// Reduces English words to their base (stem) form.
/// Based on: https://tartarus.org/martin/PorterStemmer/
/// </summary>
public class PorterEnglishStemmer : IStemmer
{
    /// <inheritdoc />
    public string Stem(string word)
    {
        if (string.IsNullOrWhiteSpace(word) || word.Length < 3)
            return word;

        word = word.ToLowerInvariant();
        var sb = new StringBuilder(word);

        // Porter stemmer steps, each refines the word.
        Step1a(sb);
        Step1b(sb);
        Step1c(sb);
        Step2(sb);
        Step3(sb);
        Step4(sb);
        Step5a(sb);
        Step5b(sb);

        return sb.ToString();
    }

    /// <summary>
    /// Checks if the StringBuilder ends with the given suffix.
    /// </summary>
    private static bool EndsWith(StringBuilder sb, string suffix)
    {
        if (sb.Length < suffix.Length)
            return false;
        for (int i = 0; i < suffix.Length; i++)
        {
            if (sb[sb.Length - suffix.Length + i] != suffix[i])
                return false;
        }
        return true;
    }

    /// <summary>
    /// Replaces the end of the StringBuilder (if matching oldSuffix) with the replacement string.
    /// </summary>
    private static void ReplaceEnd(StringBuilder sb, string oldSuffix, string replacement)
    {
        if (EndsWith(sb, oldSuffix))
        {
            sb.Length -= oldSuffix.Length;
            sb.Append(replacement);
        }
    }

    /// <summary>
    /// Determines if a character at a given position is a vowel (a, e, i, o, u, y).
    /// </summary>
    private static bool IsVowel(StringBuilder sb, int pos)
    {
        char c = sb[pos];
        return "aeiou".IndexOf(c) >= 0 || c == 'y' && pos > 0 && !"aeiou".Contains(sb[pos - 1]);
    }

    /// <summary>
    /// Step 1a: Deals with plurals and -ed/-ing.
    /// </summary>
    private static void Step1a(StringBuilder sb)
    {
        if (EndsWith(sb, "sses")) ReplaceEnd(sb, "sses", "ss");
        else if (EndsWith(sb, "ies")) ReplaceEnd(sb, "ies", "i");
        else if (EndsWith(sb, "ss")) { /* do nothing */ }
        else if (EndsWith(sb, "s")) ReplaceEnd(sb, "s", "");
    }

    /// <summary>
    /// Step 1b: Deals with -ed, -ing.
    /// </summary>
    private static void Step1b(StringBuilder sb)
    {
        if (EndsWith(sb, "eed"))
        {
            if (Measure(sb) > 0)
                ReplaceEnd(sb, "eed", "ee");
        }
        else if (EndsWith(sb, "ed") && ContainsVowel(sb, "ed") ||
                 EndsWith(sb, "ing") && ContainsVowel(sb, "ing"))
        {
            if (EndsWith(sb, "ed")) ReplaceEnd(sb, "ed", "");
            if (EndsWith(sb, "ing")) ReplaceEnd(sb, "ing", "");

            if (EndsWith(sb, "at")) sb.Append("e");
            else if (EndsWith(sb, "bl")) sb.Append("e");
            else if (EndsWith(sb, "iz")) sb.Append("e");
            else if (DoubleConsonant(sb))
                sb.Length--;
            else if (Measure(sb) == 1 && Cvc(sb))
                sb.Append("e");
        }
    }

    /// <summary>
    /// Step 1c: Turns terminal y to i if preceded by a consonant.
    /// </summary>
    private static void Step1c(StringBuilder sb)
    {
        if (EndsWith(sb, "y") && sb.Length > 2 && !IsVowel(sb, sb.Length - 2))
            ReplaceEnd(sb, "y", "i");
    }

    /// <summary>
    /// Step 2: Maps double suffixes to single ones (e.g., -ization -> -ize).
    /// </summary>
    private static void Step2(StringBuilder sb)
    {
        string[,] suffixes = {
            { "ational", "ate" }, { "tional", "tion" }, { "enci", "ence" }, { "anci", "ance" }, { "izer", "ize" },
            { "bli", "ble" }, { "alli", "al" }, { "entli", "ent" }, { "eli", "e" }, { "ousli", "ous" },
            { "ization", "ize" }, { "ation", "ate" }, { "ator", "ate" }, { "alism", "al" }, { "iveness", "ive" },
            { "fulness", "ful" }, { "ousness", "ous" }, { "aliti", "al" }, { "iviti", "ive" }, { "biliti", "ble" },
            { "logi", "log" }
        };

        for (int i = 0; i < suffixes.GetLength(0); i++)
        {
            string suffix = suffixes[i, 0];
            if (EndsWith(sb, suffix) && Measure(sb) > 0)
            {
                ReplaceEnd(sb, suffix, suffixes[i, 1]);
                break;
            }
        }
    }

    /// <summary>
    /// Step 3: Deals with further suffixes.
    /// </summary>
    private static void Step3(StringBuilder sb)
    {
        string[,] suffixes = {
            { "icate", "ic" }, { "ative", "" }, { "alize", "al" }, { "iciti", "ic" },
            { "ical", "ic" }, { "ful", "" }, { "ness", "" }
        };
        for (int i = 0; i < suffixes.GetLength(0); i++)
        {
            string suffix = suffixes[i, 0];
            if (EndsWith(sb, suffix) && Measure(sb) > 0)
            {
                ReplaceEnd(sb, suffix, suffixes[i, 1]);
                break;
            }
        }
    }

    /// <summary>
    /// Step 4: Removes -ant, -ence, etc., if measure > 1.
    /// </summary>
    private static void Step4(StringBuilder sb)
    {
        string[] suffixes = { "al", "ance", "ence", "er", "ic", "able", "ible", "ant", "ement",
            "ment", "ent", "ion", "ou", "ism", "ate", "iti", "ous", "ive", "ize" };
        foreach (var suffix in suffixes)
        {
            if (EndsWith(sb, suffix) && Measure(sb) > 1)
            {
                if (suffix == "ion")
                {
                    if (sb.Length > 3 && (sb[sb.Length - 4] == 's' || sb[sb.Length - 4] == 't'))
                    {
                        sb.Length -= suffix.Length;
                        break;
                    }
                }
                else
                {
                    sb.Length -= suffix.Length;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Step 5a: Removes final -e if measure > 1.
    /// </summary>
    private static void Step5a(StringBuilder sb)
    {
        if (EndsWith(sb, "e"))
        {
            int m = Measure(sb);
            if (m > 1 || m == 1 && !Cvc(sb, sb.Length - 2))
                sb.Length--;
        }
    }

    /// <summary>
    /// Step 5b: Remove double "l" if measure > 1.
    /// </summary>
    private static void Step5b(StringBuilder sb)
    {
        if (EndsWith(sb, "ll") && Measure(sb) > 1)
            sb.Length--;
    }

    // ---- Helper logic ----

    /// <summary>
    /// Counts the number of VC (vowel-consonant) sequences.
    /// </summary>
    private static int Measure(StringBuilder sb)
    {
        int m = 0;
        bool vowelSeen = false;
        for (int i = 0; i < sb.Length; i++)
        {
            if (IsVowel(sb, i))
            {
                if (!vowelSeen)
                {
                    vowelSeen = true;
                    m++;
                }
            }
            else
            {
                vowelSeen = false;
            }
        }
        return m;
    }

    /// <summary>
    /// Checks if there is a vowel in the stem before the suffix.
    /// </summary>
    private static bool ContainsVowel(StringBuilder sb, string suffix)
    {
        int len = sb.Length - suffix.Length;
        for (int i = 0; i < len; i++)
            if (IsVowel(sb, i)) return true;
        return false;
    }

    /// <summary>
    /// Checks if the word ends with a double consonant.
    /// </summary>
    private static bool DoubleConsonant(StringBuilder sb)
    {
        if (sb.Length < 2) return false;
        char last = sb[sb.Length - 1];
        return last == sb[sb.Length - 2] && !IsVowel(sb, sb.Length - 1);
    }

    /// <summary>
    /// Checks for the cvc pattern at the end of the word.
    /// </summary>
    private static bool Cvc(StringBuilder sb, int idx = -1)
    {
        if (sb.Length < 3) return false;
        if (idx == -1) idx = sb.Length - 1;
        return !IsVowel(sb, idx) && IsVowel(sb, idx - 1) && !IsVowel(sb, idx - 2) &&
               "wxy".IndexOf(sb[idx]) < 0;
    }
}
