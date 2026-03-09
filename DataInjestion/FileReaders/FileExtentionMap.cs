namespace Chunking.FileReaders
{
    internal static class FileExtentionMap
    {
        public static Dictionary<string, IFileReader> FileReaders = new Dictionary<string, IFileReader>
        {
            {"pdf", new PdfFileReader() },
            {"txt", new TextFileReader() },
            {"md", new TextFileReader() }
        };
    }
}
