namespace DataIngestion.FileReaders
{
    internal static class FileReaderMap
    {
        public static Dictionary<string, IFileReader> FileReaders = new Dictionary<string, IFileReader>
        {
            {"pdf", new PdfFileReader() },
            {"txt", new TextFileReader() },
            {"md", new TextFileReader() }
        };
    }
}
