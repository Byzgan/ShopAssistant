// ShopAssistant.Tests/Helpers/TestConfig.cs
namespace ShopAssistant.Tests.Helpers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

public static class TestConfig
{
    /// <summary>
    /// Creates an in-memory IConfiguration and a temporary embeddings directory
    /// with a minimal kb_meta_{language}.json file matching QuestionAnswerMappingStore expectations
    /// (top-level JSON array of { QuestionId, KnowledgeId }).
    /// </summary>
    public static (IConfiguration Config, string EmbeddingsDir) CreateWithTempEmbeddings(
        IEnumerable<(int QuestionId, int KnowledgeId)>? mappings = null,
        string language = "en")
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ShopAssistantTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var metaPath = Path.Combine(tempRoot, $"kb_meta_{language}.json");

        var list = new List<object>();
        if (mappings is null)
        {
            // sensible default: one mapping (QID 2001 -> KID 10)
            list.Add(new { QuestionId = 2001, KnowledgeId = 10 });
        }
        else
        {
            foreach (var (qid, kid) in mappings)
                list.Add(new { QuestionId = qid, KnowledgeId = kid });
        }

        File.WriteAllText(metaPath, JsonSerializer.Serialize(list));

        var dict = new Dictionary<string, string?>
        {
            ["Languages:Default"] = language,
            ["Languages:Supported:0"] = language,
            ["EmbeddingsPath"] = tempRoot
        };

        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(dict!)
            .Build();

        return (cfg, tempRoot);
    }

    /// <summary>
    /// Overload used by tests that pass a dictionary mapping QuestionId -> KnowledgeId.
    /// This preserves existing callsites and fixes CS1503.
    /// </summary>
    public static (IConfiguration Config, string EmbeddingsDir) CreateWithTempEmbeddings(
        IDictionary<int, int> mappings,
        string language = "en")
        => CreateWithTempEmbeddings(mappings?.Select(kv => (QuestionId: kv.Key, KnowledgeId: kv.Value)), language);
}
