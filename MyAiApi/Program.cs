using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Connectors.Chroma;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;

var builder = WebApplication.CreateBuilder(args);

// 1. Unify the Endpoint: Use the ENV var or a reliable default
var aiEndpoint = builder.Configuration["AI_ENDPOINT"] ?? "http://ollama_service:11434";
var chromaEndpoint = "http://chromadb:8000";

// Log it so you can see it in 'docker logs dotnet_api'
Console.WriteLine($"[Config] Connecting to Ollama at: {aiEndpoint}");

// 2. Setup the Generator with the unified endpoint
IEmbeddingGenerator<string, Embedding<float>> generator =
    new OllamaEmbeddingGenerator(new Uri(aiEndpoint), "nomic-embed-text:latest");

// 3. Setup Chroma Store
#pragma warning disable SKEXP0020 
var chromaStore = new ChromaMemoryStore(chromaEndpoint);
#pragma warning restore SKEXP0020

// 4. Build Memory using the Bridge
#pragma warning disable SKEXP0001 
var memory = new MemoryBuilder()
    .WithTextEmbeddingGeneration(generator.AsTextEmbeddingGenerationService())
    .WithMemoryStore(chromaStore)
    .Build();

builder.Services.AddSingleton<ISemanticTextMemory>(memory);
#pragma warning restore SKEXP0001

var app = builder.Build();

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
app.MapPost("/upload", async (
    IFormFile file,
    [FromServices] ISemanticTextMemory memory) =>
{
    // 1. Validation
    if (file == null || file.Length == 0)
        return Results.BadRequest("No file uploaded.");

    // 2. Read the file content
    string fullText;
    using (var reader = new StreamReader(file.OpenReadStream()))
    {
        fullText = await reader.ReadToEndAsync();
    }

    // 3. Chunk the text (using your existing ChunkText helper)
    var chunks = ChunkText(fullText, 1000, 100);

    // 4. Save to Chroma via Semantic Kernel
    // SK handles the embedding generation and the Chroma 'Add' call internally
    foreach (var chunk in chunks)
    {
        await memory.SaveInformationAsync(
            collection: "knowledge_base",
            text: chunk,
            id: Guid.NewGuid().ToString(),
            description: file.FileName // Useful for metadata/searching later
        );
    }

    return Results.Ok(new
    {
        Message = $"Successfully indexed {file.FileName}",
        Chunks = chunks.Count
    });
})
.DisableAntiforgery();
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

// Helper Method for Chunking
List<string> ChunkText(string text, int chunkSize, int overlap)
{
    var chunks = new List<string>();
    for (int i = 0; i < text.Length; i += chunkSize - overlap)
    {
        chunks.Add(text.Substring(i, Math.Min(chunkSize, text.Length - i)));
    }
    return chunks;
}

app.Run();