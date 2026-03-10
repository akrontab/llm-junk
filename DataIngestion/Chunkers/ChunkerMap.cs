namespace DataIngestion.Chunkers
{
    internal static class ChunkerMap
    {
        public static Dictionary<string, IChunker> Chunkers = new()
        {
            {"txt", new ParagraphChunker() },
            {"pdf", new ParagraphChunker() },
            {"md", new MarkdownChunker() }
        };
    }
}
