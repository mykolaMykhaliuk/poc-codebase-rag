namespace CodebaseRag.Api.Contracts.Responses;

public class HealthResponse
{
    public required string Status { get; set; }
    public string? Version { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ReadyResponse
{
    public required string Status { get; set; }
    public required Dictionary<string, string> Checks { get; set; }
}
