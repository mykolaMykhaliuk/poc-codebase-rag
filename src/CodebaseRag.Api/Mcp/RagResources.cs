using System.Text.Json;
using CodebaseRag.Api.Configuration;
using CodebaseRag.Api.Services;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace CodebaseRag.Api.Mcp;

/// <summary>
/// MCP resource providers for the Codebase RAG system.
/// Resources provide read-only access to system state and configuration.
/// </summary>
public class RagResourceProvider
{
    private readonly IIndexStatusService _indexStatusService;
    private readonly IVectorStore _vectorStore;
    private readonly RagSettings _settings;

    public RagResourceProvider(
        IIndexStatusService indexStatusService,
        IVectorStore vectorStore,
        IOptions<RagSettings> settings)
    {
        _indexStatusService = indexStatusService;
        _vectorStore = vectorStore;
        _settings = settings.Value;
    }

    /// <summary>
    /// Gets the list of available resources.
    /// </summary>
    public IEnumerable<McpResource> GetResources()
    {
        yield return new McpResource
        {
            Uri = "rag://index/status",
            Name = "Index Status",
            Description = "Current status of the codebase index including last rebuild time and statistics",
            MimeType = "application/json"
        };

        yield return new McpResource
        {
            Uri = "rag://index/files",
            Name = "Indexed Files",
            Description = "List of all files that have been indexed with their language and chunk counts",
            MimeType = "application/json"
        };

        yield return new McpResource
        {
            Uri = "rag://config/settings",
            Name = "RAG Settings",
            Description = "Current RAG system configuration including embedding model, chunk sizes, and exclusions",
            MimeType = "application/json"
        };

        yield return new McpResource
        {
            Uri = "rag://activity/recent",
            Name = "Recent Activity",
            Description = "Recent activity log including queries and index rebuilds",
            MimeType = "application/json"
        };
    }

    /// <summary>
    /// Reads a resource by URI.
    /// </summary>
    public async Task<string> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        return uri switch
        {
            "rag://index/status" => await GetIndexStatusAsync(cancellationToken),
            "rag://index/files" => GetIndexedFiles(),
            "rag://config/settings" => GetSettings(),
            "rag://activity/recent" => GetRecentActivity(),
            _ => JsonSerializer.Serialize(new { error = $"Unknown resource: {uri}" })
        };
    }

    private async Task<string> GetIndexStatusAsync(CancellationToken cancellationToken)
    {
        var status = _indexStatusService.GetStatus();
        var collectionExists = await _vectorStore.CollectionExistsAsync(cancellationToken);
        long pointCount = 0;

        if (collectionExists)
        {
            try
            {
                pointCount = await _vectorStore.GetPointCountAsync(cancellationToken);
            }
            catch
            {
                // Ignore errors getting point count
            }
        }

        var stats = await _indexStatusService.GetStatisticsAsync(cancellationToken);

        var response = new
        {
            isReady = status.IsReady,
            indexExists = collectionExists,
            totalChunks = pointCount,
            filesProcessed = status.FilesProcessed,
            lastRebuildTime = status.LastRebuildTime?.ToString("O"),
            lastRebuildDuration = status.LastRebuildDuration?.TotalSeconds,
            lastErrors = status.LastErrors,
            statistics = new
            {
                filesByLanguage = stats.FilesByLanguage,
                chunksBySymbolType = stats.ChunksBySymbolType
            }
        };

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }

    private string GetIndexedFiles()
    {
        var files = _indexStatusService.GetIndexedFiles().Select(f => new
        {
            path = f.FilePath,
            language = f.Language,
            chunkCount = f.ChunkCount,
            indexedAt = f.IndexedAt.ToString("O")
        }).ToList();

        return JsonSerializer.Serialize(new { totalFiles = files.Count, files }, new JsonSerializerOptions { WriteIndented = true });
    }

    private string GetSettings()
    {
        var response = new
        {
            codebase = new
            {
                rootPath = _settings.Codebase.RootPath,
                excludedFolders = _settings.Codebase.ExcludedFolders,
                excludedPatterns = _settings.Codebase.ExcludedFiles
            },
            embedding = new
            {
                model = _settings.Embedding.Model,
                dimensions = _settings.Embedding.Dimensions,
                batchSize = _settings.Embedding.BatchSize
            },
            chunking = new
            {
                maxChunkSize = _settings.Chunking.MaxChunkSize,
                overlap = _settings.Chunking.ChunkOverlap
            },
            prompt = new
            {
                maxContextChunks = _settings.Prompt.MaxContextChunks,
                maxContextTokens = _settings.Prompt.MaxContextTokens
            },
            parserMapping = _settings.ParserMapping
        };

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }

    private string GetRecentActivity()
    {
        var activity = _indexStatusService.GetRecentActivity(20).Select(a => new
        {
            timestamp = a.Timestamp.ToString("O"),
            action = a.Action,
            details = a.Details
        }).ToList();

        return JsonSerializer.Serialize(new { count = activity.Count, activity }, new JsonSerializerOptions { WriteIndented = true });
    }
}

/// <summary>
/// Represents an MCP resource definition.
/// </summary>
public class McpResource
{
    public required string Uri { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string MimeType { get; set; } = "text/plain";
}
