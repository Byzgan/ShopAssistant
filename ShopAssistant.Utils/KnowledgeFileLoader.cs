namespace ShopAssistant.Utils;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Contracts.Models.KnowledgeBase;

/// <summary>
/// Utility for reading knowledge items from JSON files in the localization directory.
/// </summary>
public static class KnowledgeFileLoader
{
    /// <summary>
    /// Reads all KnowledgeItems from JSON files in the directory specified by "KnowledgeBasePath" in appsettings.json.
    /// Each file must contain a JSON array of KnowledgeItemDto objects.
    /// </summary>
    /// <returns>List of KnowledgeItem objects.</returns>
    public static async Task<List<KnowledgeItem>> ReadAllKnowledgeItemsFromJsonAsync()
    {
        var knowledgeBasePath = GetKnowledgeBasePath();

        if (!Directory.Exists(knowledgeBasePath))
            throw new DirectoryNotFoundException($"KnowledgeBase folder not found: {knowledgeBasePath}");

        var result = new List<KnowledgeItem>();
        var jsonFiles = Directory.GetFiles(knowledgeBasePath, "knowledge.*.*.json", SearchOption.TopDirectoryOnly);

        if (jsonFiles.Length == 0)
        {
            Console.WriteLine("No JSON files found in the localization directory.");
            return result;
        }

        foreach (var filePath in jsonFiles)
        {
            Console.WriteLine(new string('-', 40));
            Console.WriteLine($"Start import file {filePath}");

            try
            {
                var knowledgeItems = await DeserializeKnowledgeItemsAsync(filePath);
                
                if (knowledgeItems is { Count: > 0 })
                {
                    foreach (var item in knowledgeItems)
                        item.Language = item.Language.ToLowerInvariant();
                    result.AddRange(knowledgeItems);
                }

                Console.WriteLine(knowledgeItems == null
                    ? $"Serialization error or empty file {filePath}"
                    : $"Serialization done for {filePath}: Total objects={knowledgeItems.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading {filePath}: {ex.Message}");
            }
        }

        return result;
    }
   
    private static string GetKnowledgeBasePath()
    {
        string configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

        var config = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: false, reloadOnChange: false)
            .Build();
        var knowledgeBasePath = config["KnowledgeBasePath"];
        if (string.IsNullOrWhiteSpace(knowledgeBasePath))
            throw new DirectoryNotFoundException("KnowledgeBasePath is not set in appsettings.json.");

        return knowledgeBasePath;
    }

    private static async Task<List<KnowledgeItem>?> DeserializeKnowledgeItemsAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<List<KnowledgeItem>>(stream, SerializerOptions);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
}
