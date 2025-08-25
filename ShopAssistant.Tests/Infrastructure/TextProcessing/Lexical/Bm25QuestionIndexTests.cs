namespace ShopAssistant.Tests.Infrastructure.TextProcessing.Lexical;

using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;
using ShopAssistant.Infrastructure.TextProcessing.Lexical;

[TestFixture]
public class Bm25QuestionIndexTests
{
    [Test]
    public void Query_Ranks_Relevant_Ids()
    {
        var index = new Bm25QuestionIndex(new MemoryCache(new MemoryCacheOptions()));
        var questions = new List<(int QuestionId, string Text)>
        {
            (1001, "how to return an item"),
            (2001, "track my order status"),
            (3001, "warranty policy details"),
        };
        index.Build("en", questions);

        var results = index.Query("en", "order status", 3);

        // The stable invariant is that the top result should be "order status"
        Assert.That(results.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(results[0].QuestionId, Is.EqualTo(2001));
    }
}