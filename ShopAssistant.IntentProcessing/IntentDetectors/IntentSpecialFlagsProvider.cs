using ShopAssistant.Contracts.Enums;

namespace ShopAssistant.IntentProcessing.IntentDetectors;

/// <summary>
/// Provides special flag(s) for intents based on keywords/phrases in message text.
/// Centralizes detection of "latest order" and similar multi-turn intent scenarios.
/// </summary>
public static class IntentSpecialFlagsProvider
{
    // (Intent, lang) → List of (FlagName, phrases)
    private static readonly Dictionary<(Intent, string), List<(string Flag, string[] Phrases)>> FlagPatterns = new()
    {
        {
            (Intent.OrderStatus, "en"),
            [
                ("Latest", new[] { "latest order", "last order", "most recent order", "recent order" })
            ]
        },
        {
            (Intent.OrderStatus, "no"),
            [
                ("Latest", new[] { "siste bestilling", "siste ordre", "nyeste ordre", "seneste ordre" })
            ]
        }
        
    };

    /// <summary>
    /// Detects special scenario flags for the given intent and language.
    /// Returns a dictionary of flag name to "true" if found in the message.
    /// </summary>
    public static Dictionary<string, string> GetFlags(Intent intent, string language, string message)
    {
        var flags = new Dictionary<string, string>();
        if (!FlagPatterns.TryGetValue((intent, language), out var flagDefs)) 
            return flags;

        var lowerMsg = message.ToLowerInvariant();
        foreach (var (flag, phrases) in flagDefs)
        {
            if (phrases.Any(phrase => lowerMsg.Contains(phrase)))
            {
                flags[flag] = "true";
            }
        }
        return flags;
    }
}