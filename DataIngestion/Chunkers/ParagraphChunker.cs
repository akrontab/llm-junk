using DataIngestion.Chunkers;

internal class ParagraphChunker : IChunker
{
    private readonly int _maxWordsPerChunk;
    private readonly int _overlapWords;

    public ParagraphChunker(int maxWordsPerChunk = 200, int overlapWords = 50)
    {
        _maxWordsPerChunk = maxWordsPerChunk;
        _overlapWords = overlapWords;
    }

    public List<string> CreateChunks(string rawText)
    {
        // 1. Split by double newlines to preserve paragraph integrity
        var paragraphs = rawText.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        var currentChunk = new List<string>();
        int currentWordCount = 0;

        foreach (var para in paragraphs)
        {
            var paraWordCount = para.Split(' ').Length;

            // If a single paragraph is huge, we'd need a sub-splitter, 
            // but for most docs, we treat paragraphs as atoms.
            if (currentWordCount + paraWordCount > _maxWordsPerChunk && currentChunk.Any())
            {
                chunks.Add(string.Join("\n\n", currentChunk));

                // 2. Handle Overlap: Keep the last few paragraphs for context
                // We backtrack until we have roughly our overlap requirement
                var overlapContent = new List<string>();
                int overlapCount = 0;

                for (int i = currentChunk.Count - 1; i >= 0; i--)
                {
                    var words = currentChunk[i].Split(' ').Length;
                    if (overlapCount + words <= _overlapWords)
                    {
                        overlapContent.Insert(0, currentChunk[i]);
                        overlapCount += words;
                    }
                    else break;
                }

                currentChunk = overlapContent;
                currentWordCount = overlapCount;
            }

            currentChunk.Add(para);
            currentWordCount += paraWordCount;
        }

        if (currentChunk.Any())
            chunks.Add(string.Join("\n\n", currentChunk));

        return chunks;
    }
}