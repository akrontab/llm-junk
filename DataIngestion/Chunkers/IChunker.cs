namespace DataIngestion.Chunkers
{
    internal interface IChunker
    {
        List<string> CreateChunks(string rawText);
    }
}
