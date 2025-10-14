namespace ShopAssistant.Infrastructure.TextProcessing.SemanticSearch.Embeddings;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using Contracts.Config;
using ShopAssistant.Contracts.Interfaces.TextProcessing;
using Helpers;

/// <summary>
/// Embedding generator using a local LaBSE ONNX model and WordPiece tokenizer from Microsoft.ML.Tokenizers.
/// Designed for high-throughput, fully self-hosted semantic search and vector DB use.
/// </summary>
public class LabseEmbedder : ITextEmbedder
{
    // The ONNX Runtime session for the LaBSE model; heavy-weight, should be reused (thread-safe).
    private readonly InferenceSession _session;

    private readonly HashSet<string> _inputNames;
    private readonly string _primaryOutputName;

    // The WordPiece tokenizer instance (should be constructed only once for vocab.txt efficiency).
    private readonly WordPieceTokenizer _tokenizer;

    // Special token ID for [PAD] used for padding.
    private readonly int _padTokenId;

    // Maximum number of tokens per input. The output vector is always 768 floats regardless.
    private const int MaxLength = 128;

    public LabseEmbedder(IOptions<EmbeddingsOptions> options)
    {
        var cfg = options.Value;

        // Initialize ONNX Runtime session (model is loaded once, thread-safe)
        if (!File.Exists(cfg.ModelPath))
            throw new FileNotFoundException($"ONNX model file {cfg.ModelPath} not found", cfg.ModelPath);

        // CPU-only, optimized session
        var so = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,                     // predictable on CPU
            IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount),      // kernel parallelism
            InterOpNumThreads = 1,                                            // graph-level parallelism
            EnableMemoryPattern = true
        };
        
        so.AppendExecutionProvider_CPU(); // hard-pin CPU EP
        _session = new InferenceSession(cfg.ModelPath, so);

        // NOTE: Cache IO schema (defensive feed building).
        _inputNames = _session.InputMetadata.Keys.ToHashSet(StringComparer.Ordinal);
        _primaryOutputName = _session.OutputMetadata.Keys.First();

        // Configure tokenizer options — special tokens, unknown handling, etc.
        // The token IDs must match those in your vocab.txt!
        var tokenizerOptions = new WordPieceOptions
        {
            UnknownToken = "[UNK]",
            ContinuingSubwordPrefix = "##",
            MaxInputCharsPerWord = 100,
            SpecialTokens = new Dictionary<string, int>
            {
                { "[CLS]", 101 }, // Standard IDs for bert-base-multilingual-cased
                { "[SEP]", 102 },
                { "[PAD]", 0 }
            }
        };

        // Create tokenizer. The vocab.txt is parsed only once at startup for efficiency.
        if (!File.Exists(cfg.VocabPath))
            throw new FileNotFoundException($"Vocab file {cfg.VocabPath} not found.", cfg.VocabPath);
        
        _tokenizer = WordPieceTokenizer.Create(cfg.VocabPath, tokenizerOptions);

        // Cache special token IDs for reuse in padding and pre/post-processing.
        _padTokenId = tokenizerOptions.SpecialTokens["[PAD]"];
    }

    /// <summary>
    /// Generates a dense embedding vector (float[768]) for the provided text.
    /// Supports all typical BERT/LaBSE ONNX model exports (expects input_ids, attention_mask, token_type_ids).
    /// </summary>
    /// <param name="text">Input string (any supported language).</param>
    /// <returns>A float array of size 768 — the universal sentence embedding.</returns>
    public Task<float[]> GetEmbeddingAsync(string text)
    {
        // 0. Preprocess input for consistency between user and KB questions
        text = TextPreprocessor.Clean(text);

        // 1. Tokenize input text into WordPiece token IDs
        var inputIdsRaw = _tokenizer.EncodeToIds(text); // IReadOnlyList<int>

        // 2. Pad or truncate to MaxLength (required for BERT/LaBSE input)
        long[] inputIds = new long[MaxLength];
        long[] attentionMask = new long[MaxLength];
        long[] tokenTypeIds = new long[MaxLength]; // All zeros for single-sequence mode
        int inputLen = inputIdsRaw.Count < MaxLength ? inputIdsRaw.Count : MaxLength;

        for (int i = 0; i < MaxLength; i++)
        {
            tokenTypeIds[i] = 0;
            if (i < inputLen)
            {
                inputIds[i] = inputIdsRaw[i];
                attentionMask[i] = 1; // Attend to this token
            }
            else
            {
                inputIds[i] = _padTokenId; // [PAD]
                attentionMask[i] = 0; // Mask out
            }
        }

        // 3. Prepare ONNX Runtime inputs: input_ids, attention_mask, token_type_ids
        // NOTE: Some LaBSE ONNX exports do not define token_type_ids; feed it only if present.
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(inputIds, [1, MaxLength])),
            NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(attentionMask, [1, MaxLength]))
        };
        
        if (_inputNames.Contains("token_type_ids"))
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(tokenTypeIds, [1, MaxLength])));

        // 4. Run inference
        using var results = _session.Run(inputs);
        var first = results.FirstOrDefault(x => x.Name == _primaryOutputName) ?? results.First();
        var output = first.AsTensor<float>();

        // 5. Extract the embedding
        float[] embedding;
        if (output.Dimensions.Length == 2 && output.Dimensions[0] == 1 && output.Dimensions[1] == 768)
        {
            // Typical output: [1, 768] — just flatten
            embedding = output.ToArray();
        }
        else if (output.Dimensions.Length == 3 && output.Dimensions[0] == 1 && output.Dimensions[2] == 768)
        {
            // Some models: [1, seqLen, 768] — use mean-pooling over non-padded tokens
            // NOTE: mean-pooling with attention_mask is more stable for sentence embeddings than raw [CLS].
            int seqLen = output.Dimensions[1];
            int dim = output.Dimensions[2];

            int valid = 0;
            for (int i = 0; i < inputLen; i++)
            {
                if (attentionMask[i] == 1)
                    valid++;
            }
            
            if (valid == 0) 
                valid = 1;

            embedding = new float[dim];
            for (int t = 0; t < seqLen && t < MaxLength; t++)
            {
                if (attentionMask[t] == 0) 
                    break;
                for (int d = 0; d < dim; d++)
                    embedding[d] += output[0, t, d];
            }
            for (int d = 0; d < dim; d++)
                embedding[d] /= valid;
        }
        else
        {
            // NOTE: Tensor.Dimensions is a ReadOnlySpan<int>; convert to array for string.Join.
            var dims = output.Dimensions.ToArray();
            throw new InvalidOperationException($"Unexpected output shape from LaBSE ONNX model. Please verify your export. Shape: [{string.Join(",", dims)}]");
        }

        // 6. Normalize embedding to unit length for cosine similarity consistency
        embedding = TextPreprocessor.Normalize(embedding);

        // 7. Return embedding vector
        return Task.FromResult(embedding);
    }
}