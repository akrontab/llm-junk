using DataIngestion.Chunkers;
using DataIngestion.Chunking;
using DataIngestion.Models;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Connectors.Chroma;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using System.Net.Http.Json;
using System.Text.Json;

namespace DataIngestion
{
    public class DataIngestionOrchestrator
    {
        private readonly HttpClient _http;
        private readonly string _ollamaUrl;
        private readonly string _chromaEndpoint;
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        private readonly ISemanticTextMemory _memory;
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        public DataIngestionOrchestrator(string ollamaBaseUrl = "http://localhost:11434", string chromaEndpoint = "http://localhost:8000")
        {
            _ollamaUrl = ollamaBaseUrl;
            _chromaEndpoint = chromaEndpoint;
            _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

            // TODO - Make the model configurable
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
            var fileExtension = Path.GetExtension(filename).ToLower();

            fileExtension = fileExtension.Replace(".", string.Empty);

            // 1. Extract Text
            var documentLoader = new DocumentLoader();
            string fullText = await documentLoader.LoadDocumentAsync(fileExtension, data);
            if (string.IsNullOrWhiteSpace(fullText)) return;

            // 2. Generate Global Context (The "Big Picture" Summary)
            string globalSummary = await GetGlobalSummary(filename, fullText);

            // 3. Chunk & Enhance
            // Ensure your ChunkerMap handles the extension correctly
            if (!ChunkerMap.Chunkers.TryGetValue(fileExtension, out var chunker))
            {
                // Fallback to a default chunker if extension isn't found
                chunker = ChunkerMap.Chunkers[".txt"];
            }

            var rawChunks = chunker.CreateChunks(fullText);
            var enhancer = new MetadataEnhancer();

            for (int i = 0; i < rawChunks.Count; i++)
            {
                var enhanced = enhancer.Enhance(rawChunks[i], filename, globalSummary, i);

                // 4. Store via Semantic Kernel
                // We use 'SearchableContent' for the text so the metadata is part of the vector
                await StoreInVectorDb(enhanced, filename, i);
            }
        }

        private async Task StoreInVectorDb(EnhancedChunk enhanced, string filename, int index)
        {
            await _memory.SaveInformationAsync(
                collection: "knowledge_base",
                text: enhanced.SearchableContent,
                id: $"{filename}_{index}",
                description: filename,
                additionalMetadata: JsonSerializer.Serialize(enhanced.Metadata)
            );
        }

        private async Task<string> GetGlobalSummary(string fileName, string text)
        {
            var sample = text[..Math.Min(text.Length, 3000)];
            var request = new { model = "llama3.2", prompt = $"Summarize this document in one sentence: {fileName}\nContent: {sample}", stream = false };

            var response = await _http.PostAsJsonAsync($"{_ollamaUrl}/api/generate", request);
            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>();

            return result?.Response?.Trim() ?? "General document context.";
        }
    }
}