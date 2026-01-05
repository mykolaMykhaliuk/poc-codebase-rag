using CodebaseRag.Api.Parsing;

namespace CodebaseRag.Api.Services;

public interface IVectorStore
{
    Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default);
    Task CreateCollectionAsync(int vectorSize, CancellationToken cancellationToken = default);
    Task DeleteCollectionAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(IEnumerable<CodeChunk> chunks, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScoredChunk>> SearchAsync(float[] queryVector, int limit,
        SearchFilter? filter = null, CancellationToken cancellationToken = default);
    Task<long> GetPointCountAsync(CancellationToken cancellationToken = default);
}

public class ScoredChunk
{
    public required CodeChunk Chunk { get; set; }
    public double Score { get; set; }
}

public class SearchFilter
{
    public List<string>? Languages { get; set; }
    public string? PathPrefix { get; set; }
}
