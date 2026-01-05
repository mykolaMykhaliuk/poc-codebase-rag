namespace CodebaseRag.Api.Contracts.Requests;

public class QueryRequest
{
    public required string Question { get; set; }
    public QueryOptions? Options { get; set; }
}

public class QueryOptions
{
    public int? MaxResults { get; set; }
    public List<string>? Languages { get; set; }
    public string? PathFilter { get; set; }
}
