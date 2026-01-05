namespace CodebaseRag.Api.Parsing;

public class CodeChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string FilePath { get; set; }
    public required string Language { get; set; }
    public string SymbolType { get; set; } = "unknown";
    public string? SymbolName { get; set; }
    public required string Content { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string? ParentSymbol { get; set; }
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;

    public float[]? Embedding { get; set; }
}
