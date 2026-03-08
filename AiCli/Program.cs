using Microsoft.Extensions.AI;
using System.Text;

var aiEndpoint = "http://localhost:11434";

var chatClient = new OllamaChatClient(new Uri(aiEndpoint), "llama3.2:3b")
                    .AsBuilder()
                    .UseFunctionInvocation()
                    .Build();

var plugin = new KnowledgeBasePlugin(aiEndpoint);

var chatOptions = new ChatOptions
{
    Tools = [AIFunctionFactory.Create(plugin.SearchDocuments)]
};

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("--- RAG Enabled CLI (Agentic Client) ---");
Console.ResetColor();

while (true)
{
    Console.Write("\nUser: ");
    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "exit") break;

    var contextBuilder = new StringBuilder();

    contextBuilder.AppendLine("CONTEXT:");
    contextBuilder.AppendLine("You are a research assistant. If you don't know an answer, use the SearchDocuments tool to look it up in our internal database.");
    contextBuilder.AppendLine("If no documents are returned by the tool call then try to answer as best you can, but also acknowledge no documents were found");
    contextBuilder.AppendLine();

    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("AI: ");
    Console.ResetColor();

    var prompt = contextBuilder.ToString() + $"USER PROMPT: {input}";

    await foreach (var update in chatClient.GetStreamingResponseAsync(prompt, chatOptions))
    {
        Console.Write(update.Text);
    }
    Console.WriteLine();
}