using Microsoft.Extensions.AI;
using VectorStore.Providers;

// 1. Setup Infrastructure (In a real app, this comes from builder.Services)
var aiEndpoint = "http://localhost:11434";
var vectorStoreEndpoint = "http://localhost:6334";

// 2. Initialize our Abstracted Services
var vectorStore = new QdrantVectorStore(vectorStoreEndpoint);
var embeddingGenerator = new OllamaEmbeddingGenerator(new Uri(aiEndpoint), "qwen3-embedding:latest");

// 3. Build the Agentic Chat Client
IChatClient chatClient = new OllamaChatClient(new Uri(aiEndpoint), "llama3.2:3b")
    .AsBuilder()
    .UseFunctionInvocation() // Enables the LLM to decide when to call SearchDocuments
    .Build();

// 4. Initialize the Plugin with its dependencies
var plugin = new KnowledgeBasePlugin(embeddingGenerator, vectorStore);

var chatOptions = new ChatOptions
{
    Tools = [AIFunctionFactory.Create(plugin.SearchDocuments)]
};

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("--- RAG Enabled CLI (Agentic Client) ---");
Console.WriteLine($"Connected to Qdrant at: {vectorStoreEndpoint}");
Console.ResetColor();

while (true)
{
    Console.Write("\nUser: ");
    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "exit") break;

    // Use a System Message for instructions rather than string concatenation 
    // This is the "Modern AI" way to handle context.
    var messages = new List<ChatMessage>
    {
        new(ChatRole.System, """
            You are a research assistant. 
            If you don't know an answer, use the SearchDocuments tool to look it up.
            If the tool returns no documents, answer to the best of your ability 
            but mention that no internal documents were found.
            """),
        new(ChatRole.User, input)
    };

    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("AI: ");
    Console.ResetColor();

    // The stream will now automatically handle:
    // 1. Deciding to call SearchDocuments
    // 2. Executing the search via Qdrant
    // 3. Feeding results back to the LLM
    // 4. Generating the final answer
    await foreach (var update in chatClient.GetStreamingResponseAsync(messages, chatOptions))
    {
        Console.Write(update.Text);
    }
    Console.WriteLine();
}