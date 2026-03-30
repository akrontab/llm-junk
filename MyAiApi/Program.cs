using DataIngestion;
using Microsoft.Extensions.AI;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using VectorStore.Abstractions;
using VectorStore.Providers;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Configuration & Identity ---
var serviceName = "dotnet-api";
var aiEndpoint = builder.Configuration["AI_ENDPOINT"] ?? "http://ollama_service:11434";
var vectorStoreEndpoint = builder.Configuration["VECTOR_STORE_ENDPOINT"] ?? "http://vector_store:6334";

// --- 2. OpenTelemetry (Observability) ---
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["host.name"] = Environment.MachineName
        }))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("DataIngestion.Orchestrator") // Matches your Orchestrator ActivitySource
        .AddOtlpExporter(opt => opt.Endpoint = new Uri("http://aspire-dashboard:4317")))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("DataIngestion.Orchestrator") // Matches your Orchestrator Meter
        .AddOtlpExporter(opt => opt.Endpoint = new Uri("http://aspire-dashboard:4317")));

// --- 3. Dependency Injection (DI) ---
builder.Services.AddControllers();

// A. Register Vector Store (Qdrant Implementation)
builder.Services.AddSingleton<IVectorStore>(new QdrantVectorStore(vectorStoreEndpoint));

// B. Register Ollama Services (Using Microsoft.Extensions.AI)
// This requires the 'Microsoft.Extensions.AI.Ollama' NuGet package
builder.Services.AddChatClient(new OllamaChatClient(new Uri(aiEndpoint), "llama3.2"));
builder.Services.AddEmbeddingGenerator(new OllamaEmbeddingGenerator(new Uri(aiEndpoint), "qwen3-embedding"));

// C. Register the Orchestrator (Resolved via DI)
builder.Services.AddScoped<DataIngestionOrchestrator>(sp =>
    new DataIngestionOrchestrator(
        sp.GetRequiredService<IVectorStore>(),
        sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>(),
        aiEndpoint
    ));

// --- 4. App Pipeline ---
var app = builder.Build();

// Enable Controller routing
app.MapControllers();

// Basic root check
app.MapGet("/", () => $"AI API is running. Ollama: {aiEndpoint} | VectorStore: {vectorStoreEndpoint}");

app.Run();

// Required for integration testing or internal assembly visibility
public partial class Program { }