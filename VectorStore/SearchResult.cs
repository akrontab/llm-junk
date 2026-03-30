namespace VectorStore.Abstractions;

/// <summary>
/// A standardized search result returned from any IVectorStore implementation.
/// </summary>
/// <param name="Content">The raw text or 'payload' of the document chunk.</param>
/// <param name="Score">The similarity score (usually 0.0 to 1.0).</param>
/// <param name="Metadata">Optional additional data like FileName, PageNumber, etc.</param>
public record SearchResult(
    string Content,
    float Score,
    Dictionary<string, object>? Metadata = null
);