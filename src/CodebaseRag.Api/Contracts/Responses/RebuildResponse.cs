namespace CodebaseRag.Api.Contracts.Responses;

public class RebuildResponse
{
    public bool Success { get; set; }
    public int FilesProcessed { get; set; }
    public int ChunksIndexed { get; set; }
    public List<string> Errors { get; set; } = new();
    public long DurationMs { get; set; }
}
