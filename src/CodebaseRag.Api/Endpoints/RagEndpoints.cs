using System.Diagnostics;
using CodebaseRag.Api.Configuration;
using CodebaseRag.Api.Contracts.Requests;
using CodebaseRag.Api.Contracts.Responses;
using CodebaseRag.Api.Parsing;
using CodebaseRag.Api.Services;
using Microsoft.Extensions.Options;

namespace CodebaseRag.Api.Endpoints;

public static class RagEndpoints
{
    public static void MapRagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/rag")
            .WithTags("RAG");

        group.MapPost("/rebuild", RebuildIndex)
            .WithName("RebuildIndex")
            .WithDescription("Rebuilds the entire vector index from the codebase")
            .Produces<RebuildResponse>()
            .Produces<RebuildResponse>(500);

        group.MapPost("/query", QueryCodebase)
            .WithName("QueryCodebase")
            .WithDescription("Queries the codebase and returns an LLM-ready prompt")
            .Produces<QueryResponse>()
            .Produces(400);
    }

    private static async Task<IResult> RebuildIndex(
        ICodebaseScanner scanner,
        IParserFactory parserFactory,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        IIndexStatusService indexStatusService,
        IOptions<RagSettings> settings,
        ILogger<RagEndpointsLogger> logger,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new RebuildResponse();
        var chunkingSettings = settings.Value.Chunking;

        // Clear tracked files for fresh rebuild
        if (indexStatusService is IndexStatusService statusService)
        {
            statusService.ClearIndexedFiles();
        }

        try
        {
            logger.LogInformation("Starting index rebuild");

            // Delete existing collection
            await vectorStore.DeleteCollectionAsync(cancellationToken);

            // Create new collection
            await vectorStore.CreateCollectionAsync(embeddingService.Dimensions, cancellationToken);

            // Scan files
            var files = scanner.ScanFiles().ToList();
            logger.LogInformation("Found {FileCount} files to process", files.Count);

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
                    response.FilesProcessed++;

                    // Track indexed file
                    if (indexStatusService is IndexStatusService iss)
                    {
                        iss.RecordIndexedFile(file.RelativePath, chunks.FirstOrDefault()?.Language ?? "unknown", chunks.Count);
                    }

                    logger.LogDebug("Parsed {FilePath}: {ChunkCount} chunks", file.RelativePath, chunks.Count);
                }
                catch (Exception ex)
                {
                    var error = $"Failed to parse {file.RelativePath}: {ex.Message}";
                    response.Errors.Add(error);
                    logger.LogWarning(ex, "Failed to parse file {FilePath}", file.RelativePath);
                }
            }

            logger.LogInformation("Parsed {ChunkCount} total chunks from {FileCount} files",
                allChunks.Count, response.FilesProcessed);

            // Generate embeddings in batches
            var batchSize = settings.Value.Embedding.BatchSize;
            var batches = allChunks
                .Select((chunk, index) => new { chunk, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.chunk).ToList())
                .ToList();

            logger.LogInformation("Processing {BatchCount} batches", batches.Count);

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
                    response.ChunksIndexed += batch.Count;

                    logger.LogDebug("Indexed batch of {BatchSize} chunks", batch.Count);
                }
                catch (Exception ex)
                {
                    var error = $"Failed to embed/store batch: {ex.Message}";
                    response.Errors.Add(error);
                    logger.LogError(ex, "Failed to process batch");
                }
            }

            stopwatch.Stop();
            response.DurationMs = stopwatch.ElapsedMilliseconds;
            response.Success = response.Errors.Count == 0;

            // Record rebuild in status service
            indexStatusService.RecordRebuild(
                response.FilesProcessed,
                response.ChunksIndexed,
                stopwatch.Elapsed,
                response.Errors);

            logger.LogInformation("Index rebuild completed: {FilesProcessed} files, {ChunksIndexed} chunks, {ErrorCount} errors, {DurationMs}ms",
                response.FilesProcessed, response.ChunksIndexed, response.Errors.Count, response.DurationMs);

            return response.Success
                ? Results.Ok(response)
                : Results.Json(response, statusCode: 500);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Index rebuild failed");

            response.Success = false;
            response.Errors.Add($"Fatal error: {ex.Message}");
            response.DurationMs = stopwatch.ElapsedMilliseconds;

            // Record failed rebuild
            indexStatusService.RecordRebuild(
                response.FilesProcessed,
                response.ChunksIndexed,
                stopwatch.Elapsed,
                response.Errors);

            return Results.Json(response, statusCode: 500);
        }
    }

    private static async Task<IResult> QueryCodebase(
        QueryRequest request,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        IPromptBuilder promptBuilder,
        IIndexStatusService indexStatusService,
        IOptions<RagSettings> settings,
        ILogger<RagEndpointsLogger> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return Results.BadRequest(new { error = "Question is required" });
        }

        try
        {
            var maxResults = request.Options?.MaxResults ?? settings.Value.Prompt.MaxContextChunks;

            logger.LogInformation("Processing query: {Question}", request.Question);

            // Check if collection exists
            if (!await vectorStore.CollectionExistsAsync(cancellationToken))
            {
                return Results.BadRequest(new { error = "Index not built. Call POST /rag/rebuild first." });
            }

            // Get point count
            var pointCount = await vectorStore.GetPointCountAsync(cancellationToken);
            if (pointCount == 0)
            {
                return Results.BadRequest(new { error = "Index is empty. Call POST /rag/rebuild first." });
            }

            // Embed the question
            var queryVector = await embeddingService.EmbedAsync(request.Question, cancellationToken);

            // Build search filter
            SearchFilter? filter = null;
            if (request.Options != null)
            {
                filter = new SearchFilter
                {
                    Languages = request.Options.Languages,
                    PathPrefix = request.Options.PathFilter
                };
            }

            // Search for relevant chunks
            var results = await vectorStore.SearchAsync(queryVector, maxResults, filter, cancellationToken);

            logger.LogInformation("Found {ResultCount} relevant chunks", results.Count);

            // Record query in status service
            indexStatusService.RecordQuery(request.Question, results.Count);

            // Build prompt
            var prompt = promptBuilder.BuildPrompt(request.Question, results);

            // Build response
            var response = new QueryResponse
            {
                Prompt = prompt,
                Sources = results.Select(r => new SourceInfo
                {
                    FilePath = r.Chunk.FilePath,
                    SymbolName = r.Chunk.SymbolName,
                    Lines = $"{r.Chunk.StartLine}-{r.Chunk.EndLine}",
                    RelevanceScore = Math.Round(r.Score, 4)
                }).ToList(),
                Metadata = new QueryMetadata
                {
                    ChunksSearched = (int)pointCount,
                    ChunksReturned = results.Count,
                    EmbeddingModel = embeddingService.ModelName
                }
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Query failed");
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    }
}

// Helper class for structured logging
file class RagEndpointsLogger { }
