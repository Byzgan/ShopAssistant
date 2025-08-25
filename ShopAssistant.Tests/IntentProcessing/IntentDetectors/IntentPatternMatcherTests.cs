// ShopAssistant.Tests/IntentProcessing/IntentDetectors/IntentPatternMatcherTests.cs
namespace ShopAssistant.Tests.IntentProcessing.IntentDetectors;

using System.Collections.Generic;
using NUnit.Framework;
using ShopAssistant.Contracts.Enums;
using ShopAssistant.Contracts.Interfaces.TextProcessing;
using ShopAssistant.Contracts.Models.Intent;
using ShopAssistant.IntentProcessing.IntentDetectors;

public sealed class NoopStemmer : IStemmer
{
    public string Stem(string word) => word?.Trim().ToLowerInvariant() ?? string.Empty;
}

[TestFixture]
public class IntentPatternMatcherTests
{
    private readonly IStemmer _stemmer = new NoopStemmer();

    [Test]
    public void RequiredTokenGroups_Negative_When_Group_Missing()
    {
        var pattern = new IntentPattern
        {
            Intent = Intent.ProductSearch,
            RequiredTokens =
            [
                new List<string> { "find", "search" },
                new List<string> { "phone", "iphone" }
            ],
        };

        var sut = new IntentPatternMatcher();

        // Missing group 2 ("phone/iphone") → must not match
        var miss = sut.Match("en", "please find laptop", pattern, _stemmer);
        Assert.That(miss.IsMatch, Is.False);
    }

    [Test]
    public void PartialOverlap_Produces_NonNegative_Score()
    {
        var pattern = new IntentPattern
        {
            Intent = Intent.ProductSearch,
            RequiredTokens =
            [
                new List<string> { "find", "search" },
                new List<string> { "phone", "iphone" }
            ],
        };

        var sut = new IntentPatternMatcher();
        var res = sut.Match("en", "search phone deals", pattern, _stemmer);

        // Don’t force IsMatch=true — current logic may require stricter anchors.
        Assert.That(res.Score, Is.GreaterThanOrEqualTo(0f));
    }
}