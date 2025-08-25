using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShopAssistant.Contracts.Interfaces.TextProcessing;

namespace ShopAssistant.Tests.Helpers;

public class FakeEmbedder : ITextEmbedder
{
    private static readonly Dictionary<string, float[]> Vectors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["recommend"] = [1f,0f,0f],
        ["phone"]     = [0f,1f,0f],
        ["laptop"]    = [0f,0f,1f],
        ["hello"]     = [0.1f,0.1f,0.1f],
    };

    public Task<float[]> GetEmbeddingAsync(string text)
    {
        var tokens = text.Split([' ',',','.'], StringSplitOptions.RemoveEmptyEntries);
        var v = new float[3];
        foreach (var t in tokens)
        {
            if (!Vectors.TryGetValue(t, out var tv)) 
                continue;
            for (int i=0;i<3;i++) v[i] += tv[i];
        }
        return Task.FromResult(v);
    }
}