namespace ShopAssistant.Infrastructure.TextProcessing.Intent;

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShopAssistant.Contracts.Models.Intent;

/// <summary>
/// Utility for loading intent pattern files for all supported languages.
/// Used for both cache initialization and pre-cache validation.
/// </summary>
public static class IntentPatternFileLoader
{
    /// <summary>
    /// Loads all intent pattern files from the specified directory.
    /// The directory path is typically configured in app settings.
    /// </summary>
    /// <param name="configDirectory">Directory containing intent pattern JSON files.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>
    /// Dictionary keyed by language code (e.g., "en", "de") with lists of intent patterns.
    /// </returns>
    public static async Task<Dictionary<string, List<IntentPattern>>> LoadAllIntentPatternsAsync(string configDirectory, ILogger? logger = null)
    {
        var results = new Dictionary<string, List<IntentPattern>>();

        if (!Directory.Exists(configDirectory))
        {
            logger?.LogError("Intent pattern directory does not exist: {Directory}", configDirectory);
            return results;
        }

        var files = Directory.GetFiles(configDirectory, "intents.*.json");
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var language = ExtractLanguageFromFileName(fileName);
            if (language == null)
            {
                logger?.LogWarning("Skipping intent pattern file with unrecognized name: {File}", fileName);
                continue;
            }

            try
            {
                await using var stream = File.OpenRead(file);
                var patterns = await JsonSerializer.DeserializeAsync<List<IntentPattern>>(stream) ?? new List<IntentPattern>();
                results[language] = patterns;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to load intent pattern file: {File}", fileName);
            }
        }
        return results;
    }

    /// <summary>
    /// Extracts the language code from a pattern file name (e.g., "intents.en.json" → "en").
    /// </summary>
    private static string? ExtractLanguageFromFileName(string fileName)
    {
        var parts = fileName.Split('.');
        return parts is ["intents", _, _] ? parts[1] : null;
    }
}
