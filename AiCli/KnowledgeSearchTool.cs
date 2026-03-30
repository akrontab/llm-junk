using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using VectorStore.Abstractions;

public class KnowledgeBasePlugin(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    IVectorStore vectorStore)
{
    [Description("Searches the internal knowledge base for documents related to a query.")]
    public async Task<string> SearchDocuments(
        [Description("The search query or topic to look up")] string query)
    {
        // 1. Generate the embedding for the query string
        var queryEmbedding = await embeddingGenerator.GenerateAsync(query);

        // 2. Search our abstract Vector Store
        var results = await vectorStore.SearchAsync(queryEmbedding.Vector, limit: 3);

        if (results.Count == 0)
        {
            return "No relevant documents found in the internal knowledge base.";
        }

        // 3. Format the results
        var combinedText = new StringBuilder();
        combinedText.AppendLine("I found these relevant documents in the knowledge base:");

        foreach (var result in results)
        {
            combinedText.AppendLine($"\n- {result.Content}");
        }

        return combinedText.ToString();
    }
}