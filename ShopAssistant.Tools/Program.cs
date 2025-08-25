using ShopAssistant.Infrastructure.KnowledgeBase;

namespace ShopAssistant.Tools;

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Contracts.Interfaces.TextProcessing;
using Infrastructure.TextProcessing.SemanticSearch.Embeddings;
using Utils;

internal class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: dotnet run -- [export|validate|stat]");
            return;
        }

        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        switch (args[0].ToLowerInvariant())
        {
            case "export":
                var exporter = provider.GetRequiredService<KnowledgeExporter>();
                await exporter.ExportAsync();
                break;
            case "validate":
                await ValidateKnowledgeFilesAsync();
                break;
            case "stat":
                await PrintKnowledgeStatAsync();
                break;
            default:
                Console.WriteLine("Unknown command.");
                break;
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var rootPath = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(rootPath, "appsettings.json");

        if (!File.Exists(configPath))
            throw new FileNotFoundException("appsettings.json not found.", configPath);

        var config = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: false, reloadOnChange: false)
            .Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(b => b.AddConsole());

        // KnowledgeExporter
        services.AddTransient<KnowledgeExporter>();

        // Register settings for LabseEmbedder!
        services.Configure<Contracts.Config.EmbeddingsOptions>(config.GetSection("LaBSE"));

        // Register the embedder
        services.AddSingleton<ITextEmbedder, LabseEmbedder>();
    }

    private static async Task ValidateKnowledgeFilesAsync()
    {
        var items = await KnowledgeFileLoader.ReadAllKnowledgeItemsFromJsonAsync();
        if (!items.Any())
        {
            Console.WriteLine("No knowledge files found.");
            return;
        }

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Id.ToString()) || string.IsNullOrWhiteSpace(item.Language) || item.Questions.Count == 0)
                Console.WriteLine($"Validation failed for item with ID: {item.Id}");
        }
        Console.WriteLine("Validation complete.");
    }

    private static async Task PrintKnowledgeStatAsync()
    {
        var items = await KnowledgeFileLoader.ReadAllKnowledgeItemsFromJsonAsync();
        Console.WriteLine($"Total knowledge items: {items.Count}");

        foreach (var group in items.GroupBy(x => x.Language))
        {
            var questionsCount = group.SelectMany(x => x.Questions).Count();
            Console.WriteLine($"Language '{group.Key}': {group.Count()} items, {questionsCount} questions");
        }
    }
}
