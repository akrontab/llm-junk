using DataIngestion;
using DataIngestion.Chunkers;
using DataIngestion.FileReaders;
using DataIngestion.Models;
using System.Net.Http.Json;

public class DataIngestionOrchestrator
{
    private readonly HttpClient _http;
    private readonly string _ollamaUrl;

    public DataIngestionOrchestrator(string ollamaBaseUrl = "http://localhost:11434")
    {
        _ollamaUrl = ollamaBaseUrl;
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    }

    public async Task ProcessFile(string filename, Stream data)
    {
        var fileExtension = Path.GetExtension(filename);

        // 1. Extract Text
        var fileReader = FileReaderMap.FileReaders[fileExtension];

        string fullText = await fileReader.GetFileContentAsync(data);
        if (string.IsNullOrWhiteSpace(fullText)) return;

        // 2. Generate Global Context (The "Big Picture" Summary)
        string globalSummary = await GetGlobalSummary(filename, fullText);

        // 3. Chunk & Enhance
        var chunker = ChunkerMap.Chunkers[fileExtension];

        var rawChunks = chunker.CreateChunks(fullText);

        var enhancer = new MetadataEnhancer();
        for (int i = 0; i < rawChunks.Count; i++)
        {
            var enhanced = enhancer.Enhance(rawChunks[i], filename, globalSummary, i);

            // 4. Vectorize
            var vector = await GetEmbedding(enhanced.SearchableContent);

            // 5. Store (Assuming a local ChromaDB client or API call)
            await StoreInVectorDb(vector, enhanced);
        }
    }

    private async Task<string> GetGlobalSummary(string fileName, string text)
    {
        var prompt = $"Summarize this document in one sentence for a search index. File: {fileName}\nContent: {text[..Math.Min(text.Length, 3000)]}";

        // TODO refactor the model to be configurable
        var request = new { model = "llama3.2", prompt = prompt, stream = false };
        var response = await _http.PostAsJsonAsync($"{_ollamaUrl}/api/generate", request);
        var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>();

        return result?.Response?.Trim() ?? "General document context.";
    }

    private async Task<float[]> GetEmbedding(string text)
    {
        // TODO Refactor to make the embedding model configurable
        var request = new { model = "nomic-embed-text", prompt = text };
        var response = await _http.PostAsJsonAsync($"{_ollamaUrl}/api/embeddings", request);
        var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();

        return result?.Embeddings?.FirstOrDefault() ?? Array.Empty<float>();
    }

    private async Task StoreInVectorDb(float[] vector, EnhancedChunk chunk)
    {
        // Logic to POST to your ChromaDB container (Port 8000)
        // Implementation depends on your specific Chroma wrapper
    }
}