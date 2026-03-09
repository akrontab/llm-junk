namespace DataIngestion.FileReaders
{
    /// <summary>
    /// IFileReaders provide the file content 
    /// </summary>
    internal interface IFileReader
    {
        Task<string> GetFileContentAsync(byte[] bytes);

        Task<string> GetFileContentAsync(Stream bytes);

        Task<string> GetFileContentAsync(string filePath);
    }
}