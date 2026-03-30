using DataIngestion.Chunkers;
using DataIngestion.Models;
using DataIngestion.Services;
using Microsoft.Extensions.AI;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using VectorStore.Abstractions;

namespace DataIngestion;

public class DataIngestionOrchestrator
{
    private readonly HttpClient _http;
    private readonly string _ollamaUrl;
    private readonly IVectorStore _vectorStore;
    private readonly TokenCounter _tokenCounter;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

    // --- Observability Setup ---
    private static readonly ActivitySource _source = new("DataIngestion.Orchestrator");
    private static readonly Meter _meter = new("DataIngestion.Orchestrator");
    private static readonly Histogram<int> _tokenHistogram =
        _meter.CreateHistogram<int>("ingestion.tokens_per_chunk", "tokens", "Tokens used per chunk after enhancement");

    public DataIngestionOrchestrator(
        IVectorStore vectorStore,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        string ollamaBaseUrl = "http://localhost:11434")
    {
        _vectorStore = vectorStore;
        _embeddingGenerator = embeddingGenerator;
        _ollamaUrl = ollamaBaseUrl;

        _http = new HttpClient { 
            Timeout = TimeSpan.FromMinutes(5),
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        _tokenCounter = new TokenCounter();
    }

    public async Task ProcessFile(string filename, Stream data)
    {
        using var activity = _source.StartActivity("ProcessFile");
        activity?.SetTag("file.name", filename);

        var rawExtension = Path.GetExtension(filename).ToLower();
        var fileExtension = rawExtension.Replace(".", string.Empty);

        // 1. Extract Text
        var documentLoader = new DocumentLoader();
        string fullText = await documentLoader.LoadDocumentAsync(fileExtension, data);
        if (string.IsNullOrWhiteSpace(fullText)) return;

        // 2. Generate Global Context (The "Big Picture" Summary)
        string globalSummary = await GetGlobalSummary(filename, fullText);

        // 3. Select Chunker
        if (!ChunkerMap.Chunkers.TryGetValue(fileExtension, out var chunker))
        {
            chunker = ChunkerMap.Chunkers["txt"];
        }

        var rawChunks = chunker.CreateChunks(fullText);
        var enhancer = new MetadataEnhancer();

        // 4. Processing Loop
        for (int i = 0; i < rawChunks.Count; i++)
        {
            using var loopActivity = _source.StartActivity("ProcessChunk");
            loopActivity?.SetTag("chunk.index", i);

            // Enhance with metadata
            var enhanced = enhancer.Enhance(rawChunks[i], filename, globalSummary, i);

            // Apply Token Safety Buffer (1800 for Qwen/Ollama safety)
            string safeContent = _tokenCounter.LimitToMax(enhanced.SearchableContent, 1800);

            // Record Metric
            int finalTokenCount = _tokenCounter.Count(safeContent);
            _tokenHistogram.Record(finalTokenCount, new TagList { { "file_type", fileExtension } });

            // Generate Embedding
            var embedding = await _embeddingGenerator.GenerateAsync(safeContent);

            // Create Deterministic ID (Prevents duplicates in Qdrant)
            Guid chunkId = CreateDeterministicGuid($"{filename}_{i}");

            // Store in generalized Vector Store
            await _vectorStore.UpsertAsync(
                chunkId,
                embedding.Vector,
                safeContent,
                enhanced.Metadata);
        }
    }

    private async Task<string> GetGlobalSummary(string fileName, string text)
    {
        using var activity = _source.StartActivity("GenerateGlobalSummary");

        var sample = text[..Math.Min(text.Length, 3000)];
        var request = new
        {
            model = "llama3.2:3b",
            prompt = $"Summarize this document in UNDER 30 WORDS for a search index. File: {fileName}\nContent: {sample}",
            stream = false
        };

        try
        {
            var baseUrl = _ollamaUrl.TrimEnd('/');
            var response = await _http.PostAsJsonAsync($"{baseUrl}/api/generate", request);
            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>();
            return result?.Response?.Trim() ?? "General document context.";
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return "Context unavailable.";
        }
    }

    private Guid CreateDeterministicGuid(string input)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
}