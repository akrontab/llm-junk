using DataIngestion.Chunkers;
using DataIngestion.Chunking;
using DataIngestion.Models;
using DataIngestion.Services; // Ensure this matches your TokenCounter namespace
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Connectors.Chroma;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

namespace DataIngestion
{
    public class DataIngestionOrchestrator
    {
        private readonly HttpClient _http;
        private readonly string _ollamaUrl;
        private readonly string _chromaEndpoint;
        private readonly TokenCounter _tokenCounter;
        private static readonly ActivitySource _source = new("DataIngestion.Orchestrator");

        private static readonly Meter _meter = new("DataIngestion.Orchestrator");

        // 2. Define specific instruments
        private static readonly Histogram<int> _tokenHistogram =
            _meter.CreateHistogram<int>("ingestion.tokens_per_file", "tokens", "Tokens used per file");

        private static readonly Counter<long> _totalTokens =
            _meter.CreateCounter<long>("ingestion.total_tokens", "tokens", "Total tokens processed");

#pragma warning disable SKEXP0001
        private readonly ISemanticTextMemory _memory;
#pragma warning restore SKEXP0001

        public DataIngestionOrchestrator(string ollamaBaseUrl = "http://localhost:11434", string chromaEndpoint = "http://localhost:8000")
        {
            _ollamaUrl = ollamaBaseUrl;
            _chromaEndpoint = chromaEndpoint;
            _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

            // Initialize the TokenCounter once for the lifetime of the orchestrator
            _tokenCounter = new TokenCounter();

            // Setup Embedding Generator
            IEmbeddingGenerator<string, Embedding<float>> generator =
                new OllamaEmbeddingGenerator(new Uri(_ollamaUrl), "qwen3-embedding:latest");

#pragma warning disable SKEXP0020
            var chromaStore = new ChromaMemoryStore(_chromaEndpoint);
#pragma warning restore SKEXP0020

#pragma warning disable SKEXP0001
            _memory = new MemoryBuilder()
                .WithTextEmbeddingGeneration(generator.AsTextEmbeddingGenerationService())
                .WithMemoryStore(chromaStore)
                .Build();
#pragma warning restore SKEXP0001
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

            // 3. Chunk & Enhance
            if (!ChunkerMap.Chunkers.TryGetValue(rawExtension, out var chunker))
            {
                chunker = ChunkerMap.Chunkers["txt"];
            }

            var rawChunks = chunker.CreateChunks(fullText);
            var enhancer = new MetadataEnhancer();
            try
            {
                using (var childActivity = _source.StartActivity("EmbeddingGeneration"))
                {
                    for (int i = 0; i < rawChunks.Count; i++)
                    {
                        var enhanced = enhancer.Enhance(rawChunks[i], filename, globalSummary, i);

                        // 4. APPLY SAFETY BUFFER
                        // We truncate the SearchableContent to 1800 tokens to ensure the 
                        // embedding model doesn't throw a context length error.
                        string safeContent = _tokenCounter.LimitToMax(enhanced.SearchableContent, 1800);

                        int count = _tokenCounter.Count(safeContent);

                        _tokenHistogram.Record(count, new TagList { { "file_type", fileExtension } });
                        _totalTokens.Add(count);

                        // 5. Store via Semantic Kernel
                        // We use the safeContent for vectorization but keep the enhanced metadata
                        await StoreInVectorDb(safeContent, enhanced.Metadata, filename, i);
                    }
                }
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        private async Task StoreInVectorDb(string content, Dictionary<string, object> metadata, string filename, int index)
        {
#pragma warning disable SKEXP0001
            await _memory.SaveInformationAsync(
                collection: "knowledge_base",
                text: content,
                id: $"{filename}_{index}",
                description: filename,
                additionalMetadata: JsonSerializer.Serialize(metadata)
            );
#pragma warning restore SKEXP0001
        }

        private async Task<string> GetGlobalSummary(string fileName, string text)
        {
            // We use the first 3000 chars as a representative sample
            var sample = text[..Math.Min(text.Length, 3000)];
            var request = new
            {
                model = "llama3.2",
                prompt = $"Summarize this document in UNDER 30 WORDS for a search index. File: {fileName}\nContent: {sample}",
                stream = false
            };

            var response = await _http.PostAsJsonAsync($"{_ollamaUrl}/api/generate", request);
            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>();

            return result?.Response?.Trim() ?? "General document context.";
        }
    }
}