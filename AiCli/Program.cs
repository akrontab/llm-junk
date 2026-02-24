using Microsoft.Extensions.AI;
using System.Text;

// The URL of your .NET Web API (or direct to Ollama if preferred)
// Here we point directly to the sidecar's port for the fastest response
var chatClient = new OllamaChatClient(new Uri("http://localhost:11434"), "llama3.2");

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("--- Local LLM CLI (Type 'exit' to quit) ---");
Console.ResetColor();

while (true)
{
    Console.Write("\nUser: ");
    string? input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "exit") break;

    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("AI: ");
    Console.ResetColor();

    // Use streaming for that "typing" effect
    await foreach (var update in chatClient.GetStreamingResponseAsync(input))
    {
        Console.Write(update.Text);
    }
    Console.WriteLine();
}