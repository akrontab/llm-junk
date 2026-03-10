using DataIngestion.Models;
using System.Collections.Concurrent;
using System.Net.Http.Json;

namespace DataIngestion
{
    internal class SummaryProvider
    {
        private readonly ConcurrentDictionary<string, string> _cache = new();
        private readonly HttpClient _http;

        public SummaryProvider(HttpClient http) => _http = http;

        public async Task<string> GetGlobalSummaryAsync(string serviceBaseUrl, string fileName, string fullText)
        {
            // 1. Thread-safe cache check
            if (_cache.TryGetValue(fileName, out var cachedSummary)) return cachedSummary;

            // 2. Prepare a prompt for a concise summary
            // We only take the first 4000 chars to avoid hitting context limits during summary phase
            var sampleText = fullText.Length > 4000 ? fullText[..4000] : fullText;
            var prompt = $"Summarize this document in one sentence for a search index. File: {fileName}\nContent: {sampleText}";

            var response = await _http.PostAsJsonAsync($"{serviceBaseUrl}/api/generate", new
            {
                // TODO factor model to be configurable
                model = "llama3.2", // Use a fast, small model for summaries
                prompt = prompt,
                stream = false
            });

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>();
            var summary = result?.Response?.Trim() ?? "No summary available";

            // 3. Store and return
            _cache[fileName] = summary;
            return summary;
        }
    }
}
