using Microsoft.Extensions.AI;
using ChromaDB.Client;

var builder = WebApplication.CreateBuilder(args);

var chromaConfigOptions = new ChromaConfigurationOptions("http://chromadb:8000");

// Grab the endpoint from Docker environment variables
var aiEndpoint = builder.Configuration["AI_ENDPOINT"] ?? "http://localhost:11434";

// Register the AI Client
builder.Services.AddChatClient(new OllamaChatClient(new Uri(aiEndpoint), "llama3.2"));

builder.Services.AddEmbeddingGenerator(new OllamaEmbeddingGenerator(aiEndpoint, "nomic-embed-text"));
builder.Services.AddSingleton(sp => 
{
    var config = chromaConfigOptions;
    return new ChromaClient(config, new HttpClient());
});

var app = builder.Build();

// 1. Standard Request
app.MapPost("/ask", async (string prompt, IChatClient chatClient) =>
{
    var response = await chatClient.GetResponseAsync(prompt);
    
    // In Microsoft.Extensions.AI, 'Text' is the recommended way 
    // to get the primary string content from the response.
    return Results.Ok(new { 
        Answer = response.Text, 
        Model = "llama3.2" 
    });
});

// 2. Streaming Request
app.MapGet("/stream", async (string prompt, IChatClient chatClient, HttpContext context) =>
{
    context.Response.ContentType = "text/plain";
    await foreach (var update in chatClient.GetStreamingResponseAsync(prompt))
    {
        // 'update.Text' contains the incremental chunk
        await context.Response.WriteAsync(update.Text ?? "");
        await context.Response.Body.FlushAsync();
    }
});

app.MapPost("/upload", async (
    IFormFile file, 
    IEmbeddingGenerator<string, Embedding<float>> generator,
    ChromaClient chroma) =>
{
    if (file == null || file.Length == 0) return Results.BadRequest("No file uploaded.");

    // 1. Read file content
    string fullText = "";
    using (var reader = new StreamReader(file.OpenReadStream()))
    {
        fullText = await reader.ReadToEndAsync();
    }

    // 2. Simple Chunking (e.g., chunks of 1000 characters with 100 char overlap)
    var chunks = ChunkText(fullText, 1000, 100);
    var collection = await chroma.GetOrCreateCollection("knowledge_base");

    var collectionClient = new ChromaCollectionClient(collection, chromaConfigOptions, new HttpClient());
    
    foreach (var chunk in chunks)
    {
        var embeddings = await generator.GenerateAsync([chunk]);
        
        await collectionClient.Add(
            ids: [Guid.NewGuid().ToString()],
            embeddings: [embeddings[0].Vector.ToArray()],
            metadatas: [new Dictionary<string, object> { { "content", chunk }, { "source", file.FileName } }]
        );
    }

    return Results.Ok($"Processed {file.FileName} into {chunks.Count} chunks.");
})
.DisableAntiforgery(); // Useful for testing via Postman/Curl

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