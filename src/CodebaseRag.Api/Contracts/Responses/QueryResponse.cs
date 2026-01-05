namespace CodebaseRag.Api.Contracts.Responses;

public class QueryResponse
{
    public required string Prompt { get; set; }
    public required List<SourceInfo> Sources { get; set; }
    public QueryMetadata? Metadata { get; set; }
}

public class SourceInfo
{
    public required string FilePath { get; set; }
    public string? SymbolName { get; set; }
    public string? Lines { get; set; }
    public double RelevanceScore { get; set; }
}

public class QueryMetadata
{
    public int ChunksSearched { get; set; }
    public int ChunksReturned { get; set; }
    public string? EmbeddingModel { get; set; }
}
