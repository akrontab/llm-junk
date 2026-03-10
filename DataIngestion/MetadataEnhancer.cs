namespace DataIngestion
{
    internal class MetadataEnhancer
    {
        public EnhancedChunk Enhance(string chunkText, string fileName, string globalSummary, int sequence)
        {
            // This 'searchableContent' is what actually gets turned into a vector.
            // By including the summary, every chunk becomes semantically linked to the document's main topic.
            var searchableContent =
                $"Document Summary: {globalSummary}\n" +
                $"File: {fileName} | Part: {sequence}\n" +
                $"Content: {chunkText}";

            var metadata = new Dictionary<string, object>
            {
                { "file", fileName },
                { "summary_preview", globalSummary[..Math.Min(100, globalSummary.Length)] },
                { "seq", sequence }
            };

            return new EnhancedChunk(chunkText, searchableContent, metadata);
        }
    }
}
