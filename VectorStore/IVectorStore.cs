namespace VectorStore.Abstractions
{
    public interface IVectorStore
    {
        Task UpsertAsync(Guid id, ReadOnlyMemory<float> vector, string content, Dictionary<string, object> metadata);

        Task<List<SearchResult>> SearchAsync(ReadOnlyMemory<float> vector, int limit = 3);

    }
}
