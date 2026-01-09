using CodebaseRag.Api.Components;
using CodebaseRag.Api.Configuration;
using CodebaseRag.Api.Endpoints;
using CodebaseRag.Api.Mcp;
using CodebaseRag.Api.Parsing;
using CodebaseRag.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add configuration
builder.Services.Configure<RagSettings>(
    builder.Configuration.GetSection(RagSettings.SectionName));

// Register settings as singleton for direct injection
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RagSettings>>().Value);

// Add OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Codebase RAG API",
        Version = "v1",
        Description = "A RAG (Retrieval-Augmented Generation) system for codebases. " +
                      "Indexes source code and generates LLM-ready prompts with relevant code context."
    });
});

// Add Blazor Server for Admin UI
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register parsers
builder.Services.AddSingleton<ICodeParser, PlainTextParser>();
builder.Services.AddSingleton<ICodeParser, CSharpParser>();
builder.Services.AddSingleton<ICodeParser, JavaScriptParser>();
builder.Services.AddSingleton<IParserFactory, ParserFactory>();

// Register services
builder.Services.AddSingleton<ICodebaseScanner, CodebaseScanner>();
builder.Services.AddSingleton<IVectorStore, QdrantVectorStore>();
builder.Services.AddSingleton<IPromptBuilder, PromptBuilder>();

// Register Admin UI services
builder.Services.AddSingleton<IIndexStatusService, IndexStatusService>();
builder.Services.AddSingleton<CodebaseRag.Api.Services.IConfigurationManager, CodebaseRag.Api.Services.ConfigurationManager>();

// Register HTTP client for embedding service
builder.Services.AddHttpClient<IEmbeddingService, EmbeddingService>();

// Register MCP resource provider
builder.Services.AddSingleton<RagResourceProvider>();

// Configure MCP Server with HTTP/SSE transport
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Configure middleware
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Codebase RAG API v1");
    options.RoutePrefix = "swagger";
});

// Serve static files (CSS, JS)
app.UseStaticFiles();
app.UseAntiforgery();

// Map Blazor components for Admin UI
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map API endpoints
app.MapHealthEndpoints();
app.MapRagEndpoints();

// Redirect root to Admin UI
app.MapGet("/", () => Results.Redirect("/admin/"));

// Map MCP Server endpoint (HTTP/SSE transport)
app.MapMcp("/mcp");

app.Run();
