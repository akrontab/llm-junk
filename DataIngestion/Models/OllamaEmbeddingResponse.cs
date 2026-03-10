using System.Text.Json.Serialization;

public class OllamaEmbeddingResponse
{
    [JsonPropertyName("embeddings")]
    public List<float[]> Embeddings { get; set; } = new();
}