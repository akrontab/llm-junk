using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Connectors.Chroma;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using System.ComponentModel;
using System.Text;

public class KnowledgeBasePlugin(string aiEndpoint)
{
    [Description("Searches the internal knowledge base for documents related to a query.")]
    public async Task<string> SearchDocuments(
        [Description("The search query or topic to look up")] string query)
    {
        var generator = new OllamaEmbeddingGenerator(new Uri(aiEndpoint), "qwen3-embedding:latest");

#pragma warning disable SKEXP0020 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        var chromaStore = new ChromaMemoryStore("http://localhost:8000");
#pragma warning restore SKEXP0020 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

#pragma warning disable SKEXP0001
        var memory = new MemoryBuilder()
            .WithTextEmbeddingGeneration(generator.AsTextEmbeddingGenerationService())
            .WithMemoryStore(chromaStore)
            .Build();
#pragma warning restore SKEXP0020 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        var results = memory.SearchAsync("knowledge_base", query, limit: 3);
        var combinedText = new StringBuilder();

        await foreach (var result in results)
        {
            combinedText.AppendLine($"\n- {result.Metadata.Text})";
        }

        return combinedText.Length > 0
            ? "No relevant documents found."
            : $"Found these documents:\n{combinedText}";
    }
}