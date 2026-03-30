using Qdrant.Client;
using Qdrant.Client.Grpc;
using System.Numerics;
using VectorStore.Abstractions;

namespace VectorStore.Providers;

public class QdrantVectorStore : IVectorStore
{
    private readonly QdrantClient _client;
    private const string CollectionName = "knowledge_base";
    private bool _collectionChecked = false;

    public QdrantVectorStore(string url)
    {
        // Defaulting to the gRPC port 6334 is often faster for .NET
        _client = new QdrantClient(new Uri(url));
    }

    public async Task UpsertAsync(Guid id, ReadOnlyMemory<float> vector, string content, Dictionary<string, object> metadata)
    {
        await EnsureCollectionExistsAsync((ulong)vector.Length);

        var pointId = new PointId();

        pointId.Uuid = id.ToString();

        var point = new PointStruct
        {
            Id = pointId,
            Vectors = vector.ToArray(),
            Payload =
            {
                ["content"] = content,
                ["filename"] = metadata?.GetValueOrDefault("FileName")?.ToString() ?? string.Empty
                // Add other metadata keys as needed
            }
        };

        await _client.UpsertAsync(CollectionName, new[] { point });
    }

    public async Task<List<SearchResult>> SearchAsync(ReadOnlyMemory<float> vector, int limit = 3)
    {
        var results = await _client.SearchAsync(
            CollectionName,
            vector.ToArray(),
            limit: (ulong)limit
        );

        return results.Select(r => new SearchResult(
            r.Payload["content"].StringValue,
            r.Score
        )).ToList();
    }

    private async Task EnsureCollectionExistsAsync(ulong vectorSize)
    {
        // Optimization: Only check the network once per application lifecycle
        if (_collectionChecked) return;

        var collections = await _client.ListCollectionsAsync();
        if (!collections.Contains(CollectionName))
        {
            await _client.CreateCollectionAsync(CollectionName,
                new VectorParams
                {
                    Size = vectorSize,
                    Distance = Distance.Cosine
                });

            Console.WriteLine($"[Qdrant] Created collection '{CollectionName}' with size {vectorSize}");
        }

        _collectionChecked = true;
    }

}