using System.Net.Http.Headers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;


var builder = Host.CreateApplicationBuilder(args);

// Configuration
var watchPath = "/watch_folder"; // Change this to your Linux path
var apiUploadUrl = Environment.GetEnvironmentVariable("AI_UPLOAD_URL"); // The port mapped in your docker-compose

builder.Services.AddHostedService<FileWatcherService>(sp => 
    new FileWatcherService(watchPath, apiUploadUrl));

var host = builder.Build();
host.Run();

public class FileWatcherService : BackgroundService
{
    private readonly string _path;
    private readonly string _apiUrl;
    private readonly HttpClient _httpClient;

    public FileWatcherService(string path, string apiUrl)
    {
        _path = path;
        _apiUrl = apiUrl;
        _httpClient = new HttpClient();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Directory.Exists(_path)) Directory.CreateDirectory(_path);

        var watcher = new FileSystemWatcher(_path)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            Filter = "*.txt", // Adjust based on your needs
            EnableRaisingEvents = true
        };

        watcher.Created += async (s, e) => await UploadFileAsync(e.FullPath);
        
        Console.WriteLine($"Watching for new documents in: {_path}");
        return Task.CompletedTask;
    }

    private async Task UploadFileAsync(string filePath)
    {
        try
        {
            // Wait a moment for the file stream to be released by the OS
            await Task.Delay(500); 

            using var form = new MultipartFormDataContent();
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var streamContent = new StreamContent(fileStream);
            
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            form.Add(streamContent, "file", Path.GetFileName(filePath));

            var response = await _httpClient.PostAsync(_apiUrl, form);
            
            if (response.IsSuccessStatusCode)
                Console.WriteLine($"Successfully processed: {Path.GetFileName(filePath)}");
            else
                Console.WriteLine($"Failed to upload {Path.GetFileName(filePath)}. Status: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
        }
    }
}