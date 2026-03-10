namespace DataIngestion.FileReaders
{
    /// <summary>
    /// IFileReader provides the file content 
    /// </summary>
    internal interface IFileReader
    {
        Task<string> GetFileContentAsync(Stream data);
    }
}