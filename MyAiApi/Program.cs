public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 1. Unify the Endpoint: Use the ENV var or a reliable default
        var aiEndpoint = builder.Configuration["AI_ENDPOINT"] ?? "http://ollama_service:11434";
        var chromaEndpoint = builder.Configuration["CHROMADB_ENDPOINT"] ?? "http://chromadb:8000";

        // Log it so you can see it in 'docker logs dotnet_api'
        Console.WriteLine($"[Config] Connecting to Ollama at: {aiEndpoint}");

        var app = builder.Build();

        app.MapPost("/upload", async (IFormFile file) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest("No file uploaded.");

            // 1. Initialize our self-contained Orchestrator
            var orchestrator = new DataIngestionOrchestrator(aiEndpoint, chromaEndpoint);

            // 2. Process via the new Contextual Library
            using var stream = file.OpenReadStream();
            await orchestrator.ProcessFile(file.FileName, stream);

            return Results.Ok(new { Message = $"Successfully processed {file.FileName} with contextual embeddings." });
        })
        .DisableAntiforgery();

        app.Run();
    }
}