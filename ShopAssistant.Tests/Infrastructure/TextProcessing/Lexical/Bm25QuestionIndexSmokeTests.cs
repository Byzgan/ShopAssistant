using NUnit.Framework;
using Microsoft.Extensions.Caching.Memory;
using ShopAssistant.Infrastructure.TextProcessing.Lexical;
using System.Collections.Generic;

namespace ShopAssistant.Tests.Infrastructure.TextProcessing.Lexical;

[TestFixture]
public class Bm25QuestionIndexSmokeTests
{
    [Test]
    public void Build_Then_Query_Returns_Relevant_Id()
    {
        var index = new Bm25QuestionIndex(new MemoryCache(new MemoryCacheOptions()));
        var docs = new List<(int QuestionId, string Text)>
        {
            (1, "how to return an item"),
            (2, "track my order status"),
            (3, "warranty policy details")
        };
        index.Build("en", docs);

        var results = index.Query("en", "order status", 1);
        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0].QuestionId, Is.EqualTo(2));
    }
}