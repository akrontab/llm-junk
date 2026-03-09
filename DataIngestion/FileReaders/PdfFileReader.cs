namespace DataIngestion.FileReaders
{
    internal class PdfFileReader : IFileReader
    {
        public Task<string> GetFileContentAsync(string filePath)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetFileContentAsync(byte[] bytes)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetFileContentAsync(Stream bytes)
        {
            throw new NotImplementedException();
        }
    }
}
