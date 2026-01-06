using CodebaseRag.Api.Configuration;
using CodebaseRag.Api.Contracts.Responses;
using CodebaseRag.Api.Services;
using Microsoft.Extensions.Options;

namespace CodebaseRag.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/health")
            .WithTags("Health");

        group.MapGet("", GetHealth)
            .WithName("GetHealth")
            .WithDescription("Basic health check")
            .Produces<HealthResponse>();

        group.MapGet("/ready", GetReady)
            .WithName("GetReady")
            .WithDescription("Readiness probe - checks if dependencies are available")
            .Produces<ReadyResponse>()
            .Produces(503);
    }

    private static HealthResponse GetHealth()
    {
        return new HealthResponse
        {
            Status = "healthy",
            Version = typeof(HealthEndpoints).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            Timestamp = DateTime.UtcNow
        };
    }

    private static async Task<IResult> GetReady(
        IVectorStore vectorStore,
        IOptions<RagSettings> settings)
    {
        var checks = new Dictionary<string, string>();
        var allHealthy = true;

        // Check vector store connection
        try
        {
            await vectorStore.CollectionExistsAsync();
            checks["vectorStore"] = "connected";
        }
        catch (Exception ex)
        {
            checks["vectorStore"] = $"error: {ex.Message}";
            allHealthy = false;
        }

        // Check codebase path
        var codebasePath = settings.Value.Codebase.RootPath;
        if (Directory.Exists(codebasePath))
        {
            checks["codebasePath"] = "accessible";
        }
        else
        {
            checks["codebasePath"] = "not found";
            allHealthy = false;
        }

        var response = new ReadyResponse
        {
            Status = allHealthy ? "ready" : "not ready",
            Checks = checks
        };

        return allHealthy
            ? Results.Ok(response)
            : Results.Json(response, statusCode: 503);
    }
}
