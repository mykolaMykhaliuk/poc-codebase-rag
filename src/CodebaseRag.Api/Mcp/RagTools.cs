using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using CodebaseRag.Api.Configuration;
using CodebaseRag.Api.Parsing;
using CodebaseRag.Api.Services;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace CodebaseRag.Api.Mcp;

/// <summary>
/// MCP tools for the Codebase RAG system.
/// These tools enable LLM clients to index and query codebases.
/// </summary>
public static class RagTools
{
    /// <summary>
    /// Queries the codebase and returns relevant code context for answering questions.
    /// </summary>
    [McpServerTool]
    [Description("Query the codebase to find relevant code for answering a question. Returns an LLM-ready prompt with code context and source references.")]
    public static async Task<string> QueryCodebase(
        IMcpServer server,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        IPromptBuilder promptBuilder,
        IIndexStatusService indexStatusService,
        IOptions<RagSettings> settings,
        [Description("The question or query about the codebase")] string question,
        [Description("Maximum number of code chunks to return (default: 10)")] int? maxResults = null,
        [Description("Filter by programming languages (e.g., 'csharp', 'javascript')")] string[]? languages = null,
        [Description("Filter by file path prefix (e.g., 'src/Services')")] string? pathFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return JsonSerializer.Serialize(new { error = "Question is required" });
        }

        try
        {
            var limit = maxResults ?? settings.Value.Prompt.MaxContextChunks;

            // Check if collection exists
            if (!await vectorStore.CollectionExistsAsync(cancellationToken))
            {
                return JsonSerializer.Serialize(new { error = "Index not built. Call rebuild_index tool first." });
            }

            // Get point count
            var pointCount = await vectorStore.GetPointCountAsync(cancellationToken);
            if (pointCount == 0)
            {
                return JsonSerializer.Serialize(new { error = "Index is empty. Call rebuild_index tool first." });
            }

            // Embed the question
            var queryVector = await embeddingService.EmbedAsync(question, cancellationToken);

            // Build search filter
            SearchFilter? filter = null;
            if (languages?.Length > 0 || !string.IsNullOrEmpty(pathFilter))
            {
                filter = new SearchFilter
                {
                    Languages = languages?.ToList(),
                    PathPrefix = pathFilter
                };
            }

            // Search for relevant chunks
            var results = await vectorStore.SearchAsync(queryVector, limit, filter, cancellationToken);

            // Record query in status service
            indexStatusService.RecordQuery(question, results.Count);

            // Build prompt
            var prompt = promptBuilder.BuildPrompt(question, results);

            // Build response
            var response = new
            {
                prompt = prompt,
                sources = results.Select(r => new
                {
                    filePath = r.Chunk.FilePath,
                    symbolName = r.Chunk.SymbolName,
                    lines = $"{r.Chunk.StartLine}-{r.Chunk.EndLine}",
                    relevanceScore = Math.Round(r.Score, 4)
                }).ToList(),
                metadata = new
                {
                    chunksSearched = pointCount,
                    chunksReturned = results.Count,
                    embeddingModel = embeddingService.ModelName
                }
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Rebuilds the entire vector index from the codebase.
    /// </summary>
    [McpServerTool]
    [Description("Rebuild the codebase vector index. This scans all source files, generates embeddings, and stores them for semantic search. Use when the codebase has changed or for initial setup.")]
    public static async Task<string> RebuildIndex(
        IMcpServer server,
        ICodebaseScanner scanner,
        IParserFactory parserFactory,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        IIndexStatusService indexStatusService,
        IOptions<RagSettings> settings,
        [Description("Force rebuild even if index exists (default: true)")] bool force = true,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var filesProcessed = 0;
        var chunksIndexed = 0;
        var errors = new List<string>();
        var chunkingSettings = settings.Value.Chunking;

        // Clear tracked files for fresh rebuild
        if (indexStatusService is IndexStatusService statusService)
        {
            statusService.ClearIndexedFiles();
        }

        try
        {
            // Delete existing collection
            await vectorStore.DeleteCollectionAsync(cancellationToken);

            // Create new collection
            await vectorStore.CreateCollectionAsync(embeddingService.Dimensions, cancellationToken);

            // Scan files
            var files = scanner.ScanFiles().ToList();

            var allChunks = new List<CodeChunk>();

            // Parse files
            foreach (var file in files)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file.FullPath, cancellationToken);
                    var parser = parserFactory.GetParser(file.Extension);
                    var chunks = parser.Parse(file.RelativePath, content, chunkingSettings).ToList();
                    allChunks.AddRange(chunks);
                    filesProcessed++;

                    // Track indexed file
                    if (indexStatusService is IndexStatusService iss)
                    {
                        iss.RecordIndexedFile(file.RelativePath, chunks.FirstOrDefault()?.Language ?? "unknown", chunks.Count);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to parse {file.RelativePath}: {ex.Message}");
                }
            }

            // Generate embeddings in batches
            var batchSize = settings.Value.Embedding.BatchSize;
            var batches = allChunks
                .Select((chunk, index) => new { chunk, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.chunk).ToList())
                .ToList();

            foreach (var batch in batches)
            {
                try
                {
                    // Get embeddings for batch
                    var texts = batch.Select(c => c.Content).ToList();
                    var embeddings = await embeddingService.EmbedBatchAsync(texts, cancellationToken);

                    // Assign embeddings to chunks
                    for (var i = 0; i < batch.Count; i++)
                    {
                        batch[i].Embedding = embeddings[i];
                    }

                    // Upsert to vector store
                    await vectorStore.UpsertAsync(batch, cancellationToken);
                    chunksIndexed += batch.Count;
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to embed/store batch: {ex.Message}");
                }
            }

            stopwatch.Stop();

            // Record rebuild in status service
            indexStatusService.RecordRebuild(filesProcessed, chunksIndexed, stopwatch.Elapsed, errors);

            var response = new
            {
                success = errors.Count == 0,
                filesProcessed,
                chunksIndexed,
                errors,
                durationMs = stopwatch.ElapsedMilliseconds
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record failed rebuild
            errors.Add($"Fatal error: {ex.Message}");
            indexStatusService.RecordRebuild(filesProcessed, chunksIndexed, stopwatch.Elapsed, errors);

            var response = new
            {
                success = false,
                filesProcessed,
                chunksIndexed,
                errors,
                durationMs = stopwatch.ElapsedMilliseconds
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    /// <summary>
    /// Gets the current health and status of the RAG system.
    /// </summary>
    [McpServerTool]
    [Description("Check the health and status of the codebase RAG system, including index statistics and Qdrant connection status.")]
    public static async Task<string> GetHealth(
        IMcpServer server,
        IVectorStore vectorStore,
        IIndexStatusService indexStatusService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var status = indexStatusService.GetStatus();
            var collectionExists = await vectorStore.CollectionExistsAsync(cancellationToken);
            long pointCount = 0;

            if (collectionExists)
            {
                pointCount = await vectorStore.GetPointCountAsync(cancellationToken);
            }

            var response = new
            {
                status = "healthy",
                qdrantConnected = true,
                indexExists = collectionExists,
                chunkCount = pointCount,
                lastRebuild = status.LastRebuildTime?.ToString("O"),
                lastRebuildDuration = status.LastRebuildDuration?.TotalSeconds,
                filesIndexed = status.FilesProcessed,
                lastErrors = status.LastErrors
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            var response = new
            {
                status = "unhealthy",
                qdrantConnected = false,
                error = ex.Message
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    /// <summary>
    /// Gets detailed statistics about the index.
    /// </summary>
    [McpServerTool]
    [Description("Get detailed statistics about the indexed codebase, including file counts by language and chunk types.")]
    public static async Task<string> GetIndexStats(
        IMcpServer server,
        IIndexStatusService indexStatusService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await indexStatusService.GetStatisticsAsync(cancellationToken);
            var files = indexStatusService.GetIndexedFiles().ToList();

            var response = new
            {
                totalChunks = stats.TotalChunks,
                totalFiles = files.Count,
                filesByLanguage = stats.FilesByLanguage,
                chunksBySymbolType = stats.ChunksBySymbolType,
                indexedFiles = files.Select(f => new
                {
                    path = f.FilePath,
                    language = f.Language,
                    chunks = f.ChunkCount
                }).ToList()
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}
