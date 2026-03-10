internal record EnhancedChunk(
    string RawContent,
    string SearchableContent, // This includes the metadata "injection"
    Dictionary<string, object> Metadata
);