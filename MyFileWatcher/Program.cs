using System.Net.Http.Headers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
var watchPath = "/watch_folder";
var apiUploadUrl = Environment.GetEnvironmentVariable("AI_UPLOAD_URL");

builder.Services.AddHostedService<FileWatcherService>(sp =>
    new FileWatcherService(watchPath, apiUploadUrl));

var host = builder.Build();
host.Run();

public class FileWatcherService : BackgroundService
{
    private readonly string _path;
    private readonly string _apiUrl;
    private readonly HttpClient _httpClient;
    // Track files we've already handled to avoid infinite loops
    private readonly HashSet<string> _knownFiles = new();

    public FileWatcherService(string path, string apiUrl)
    {
        _path = path;
        _apiUrl = apiUrl;
        _httpClient = new HttpClient();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Directory.Exists(_path)) Directory.CreateDirectory(_path);

        Console.WriteLine($"Polling for new documents in: {_path} every 2 seconds...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var currentFiles = Directory.GetFiles(_path);

                foreach (var filePath in currentFiles)
                {
                    // If we haven't seen this file before, upload it
                    if (!_knownFiles.Contains(filePath))
                    {
                        Console.WriteLine($"New file detected: {Path.GetFileName(filePath)}");
                        bool success = await UploadFileAsync(filePath);

                        if (success)
                        {
                            _knownFiles.Add(filePath);
                        }
                    }
                }

                // Cleanup: Remove files from _knownFiles if they were deleted from the folder
                _knownFiles.RemoveWhere(f => !File.Exists(f));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during poll: {ex.Message}");
            }

            // Wait for 2 seconds before the next scan
            await Task.Delay(2000, stoppingToken);
        }
    }

    private async Task<bool> UploadFileAsync(string filePath)
    {
        try
        {
            // Small delay to ensure the file isn't still being written by the OS
            await Task.Delay(500);

            using var form = new MultipartFormDataContent();
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var streamContent = new StreamContent(fileStream);

            streamContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            form.Add(streamContent, "file", Path.GetFileName(filePath));

            var response = await _httpClient.PostAsync(_apiUrl, form);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Successfully processed: {Path.GetFileName(filePath)}");
                return true;
            }

            Console.WriteLine($"Failed to upload {Path.GetFileName(filePath)}. Status: {response.StatusCode}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
            return false;
        }
    }
}