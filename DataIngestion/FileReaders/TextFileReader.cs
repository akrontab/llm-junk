using System.Text;

namespace DataIngestion.FileReaders
{
    internal class TextFileReader : IFileReader
    {
        public async Task<string> GetFileContentAsync(Stream data)
        {
            // Ensure the stream is at the beginning if possible
            if (data.CanSeek && data.Position > 0)
            {
                data.Position = 0;
            }

            // Using 'leaveOpen: true' is a good practice if the caller 
            // (like the Orchestrator) manages the stream's lifecycle.
            using var reader = new StreamReader(data, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            
            return await reader.ReadToEndAsync();
        }
    }
}
