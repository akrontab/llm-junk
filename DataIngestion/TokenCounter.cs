using Microsoft.ML.Tokenizers;

namespace DataIngestion.Services;

internal class TokenCounter
{
    private readonly Tokenizer _tokenizer;

    public TokenCounter()
    {
        // Modern v1.0+ syntax using the specific Tiktoken implementation
        _tokenizer = TiktokenTokenizer.CreateForModel("gpt-4");
    }

    public int Count(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        return _tokenizer.CountTokens(text);
    }

    public string LimitToMax(string text, int maxTokens)
    {
        // GetIndexByTokenCount is the most efficient way to truncate 
        // without the overhead of full encoding/decoding.
        var trimIndex = _tokenizer.GetIndexByTokenCount(text, maxTokens, out _, out _);

        if (trimIndex >= text.Length)
            return text;

        return text.Substring(0, trimIndex);
    }
}