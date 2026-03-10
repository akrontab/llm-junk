using System.Text;
using Markdig;
using Markdig.Syntax;

namespace DataIngestion.Chunkers
{
    internal class MarkdownChunker : IChunker
    {
        private readonly int _maxWords;

        public MarkdownChunker(int maxWords = 300)
        {
            _maxWords = maxWords;
        }

        public List<string> CreateChunks(string markdown)
        {
            var document = Markdown.Parse(markdown);
            var chunks = new List<string>();
            var currentChunk = new StringBuilder();
            string currentHeader = "General"; // Track the active H1/H2 context
            int currentWordCount = 0;

            foreach (var block in document)
            {
                // 1. Handle Headers to update context
                if (block is HeadingBlock heading)
                {
                    currentHeader = markdown.Substring(heading.Span.Start, heading.Span.Length);
                }

                // 2. Extract block text
                var blockText = markdown.Substring(block.Span.Start, block.Span.Length);
                var blockWordCount = blockText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

                // 3. Check if adding this block exceeds limit
                if (currentWordCount + blockWordCount > _maxWords && currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString().Trim());
                    currentChunk.Clear();

                    // Prepend current header to the new chunk to maintain context
                    currentChunk.AppendLine($"Context: {currentHeader}");
                    currentWordCount = currentHeader.Split(' ').Length;
                }

                currentChunk.AppendLine(blockText);
                currentChunk.AppendLine(); // Maintain spacing
                currentWordCount += blockWordCount;
            }

            if (currentChunk.Length > 0)
                chunks.Add(currentChunk.ToString().Trim());

            return chunks;
        }
    }
}
