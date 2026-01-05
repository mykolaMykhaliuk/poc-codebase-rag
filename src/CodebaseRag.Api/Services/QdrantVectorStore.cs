using CodebaseRag.Api.Configuration;
using CodebaseRag.Api.Parsing;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace CodebaseRag.Api.Services;

public class QdrantVectorStore : IVectorStore
{
    private readonly QdrantClient _client;
    private readonly string _collectionName;
    private readonly ILogger<QdrantVectorStore> _logger;

    public QdrantVectorStore(IOptions<RagSettings> settings, ILogger<QdrantVectorStore> logger)
    {
        var config = settings.Value.VectorStore;
        _collectionName = config.CollectionName;
        _logger = logger;

        _client = new QdrantClient(
            host: config.Host,
            port: config.Port,
            https: config.UseTls
        );
    }

    public async Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var collections = await _client.ListCollectionsAsync(cancellationToken);
            return collections.Any(c => c == _collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if collection exists");
            throw;
        }
    }

    public async Task CreateCollectionAsync(int vectorSize, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.CreateCollectionAsync(
                collectionName: _collectionName,
                vectorsConfig: new VectorParams
                {
                    Size = (ulong)vectorSize,
                    Distance = Distance.Cosine
                },
                cancellationToken: cancellationToken
            );

            _logger.LogInformation("Created collection {CollectionName} with vector size {VectorSize}",
                _collectionName, vectorSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create collection {CollectionName}", _collectionName);
            throw;
        }
    }

    public async Task DeleteCollectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (await CollectionExistsAsync(cancellationToken))
            {
                await _client.DeleteCollectionAsync(_collectionName, cancellationToken: cancellationToken);
                _logger.LogInformation("Deleted collection {CollectionName}", _collectionName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete collection {CollectionName}", _collectionName);
            throw;
        }
    }

    public async Task UpsertAsync(IEnumerable<CodeChunk> chunks, CancellationToken cancellationToken = default)
    {
        var points = chunks.Select(chunk => new PointStruct
        {
            Id = new PointId { Uuid = chunk.Id },
            Vectors = chunk.Embedding!,
            Payload =
            {
                ["file_path"] = chunk.FilePath,
                ["language"] = chunk.Language,
                ["symbol_type"] = chunk.SymbolType,
                ["symbol_name"] = chunk.SymbolName ?? "",
                ["content"] = chunk.Content,
                ["start_line"] = chunk.StartLine,
                ["end_line"] = chunk.EndLine,
                ["parent_symbol"] = chunk.ParentSymbol ?? "",
                ["indexed_at"] = chunk.IndexedAt.ToString("O")
            }
        }).ToList();

        if (points.Count == 0)
            return;

        try
        {
            await _client.UpsertAsync(
                collectionName: _collectionName,
                points: points,
                cancellationToken: cancellationToken
            );

            _logger.LogDebug("Upserted {Count} points to collection {CollectionName}",
                points.Count, _collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert points to collection {CollectionName}", _collectionName);
            throw;
        }
    }

    public async Task<IReadOnlyList<ScoredChunk>> SearchAsync(float[] queryVector, int limit,
        SearchFilter? filter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            Filter? qdrantFilter = null;

            if (filter != null)
            {
                var conditions = new List<Condition>();

                if (filter.Languages is { Count: > 0 })
                {
                    conditions.Add(new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "language",
                            Match = new Match
                            {
                                Any = new RepeatedStrings { Strings = { filter.Languages } }
                            }
                        }
                    });
                }

                if (!string.IsNullOrEmpty(filter.PathPrefix))
                {
                    conditions.Add(new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "file_path",
                            Match = new Match { Text = filter.PathPrefix }
                        }
                    });
                }

                if (conditions.Count > 0)
                {
                    qdrantFilter = new Filter { Must = { conditions } };
                }
            }

            var results = await _client.SearchAsync(
                collectionName: _collectionName,
                vector: queryVector,
                limit: (ulong)limit,
                filter: qdrantFilter,
                payloadSelector: true,
                cancellationToken: cancellationToken
            );

            return results.Select(r => new ScoredChunk
            {
                Score = r.Score,
                Chunk = new CodeChunk
                {
                    Id = r.Id.Uuid,
                    FilePath = r.Payload["file_path"].StringValue,
                    Language = r.Payload["language"].StringValue,
                    SymbolType = r.Payload["symbol_type"].StringValue,
                    SymbolName = string.IsNullOrEmpty(r.Payload["symbol_name"].StringValue)
                        ? null : r.Payload["symbol_name"].StringValue,
                    Content = r.Payload["content"].StringValue,
                    StartLine = (int)r.Payload["start_line"].IntegerValue,
                    EndLine = (int)r.Payload["end_line"].IntegerValue,
                    ParentSymbol = string.IsNullOrEmpty(r.Payload["parent_symbol"].StringValue)
                        ? null : r.Payload["parent_symbol"].StringValue
                }
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search in collection {CollectionName}", _collectionName);
            throw;
        }
    }

    public async Task<long> GetPointCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var info = await _client.GetCollectionInfoAsync(_collectionName, cancellationToken);
            return (long)info.PointsCount;
        }
        catch
        {
            return 0;
        }
    }
}
