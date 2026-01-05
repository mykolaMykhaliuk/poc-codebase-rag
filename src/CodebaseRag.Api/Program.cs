using CodebaseRag.Api.Configuration;
using CodebaseRag.Api.Endpoints;
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

// Register parsers
builder.Services.AddSingleton<ICodeParser, PlainTextParser>();
builder.Services.AddSingleton<ICodeParser, CSharpParser>();
builder.Services.AddSingleton<ICodeParser, JavaScriptParser>();
builder.Services.AddSingleton<IParserFactory, ParserFactory>();

// Register services
builder.Services.AddSingleton<ICodebaseScanner, CodebaseScanner>();
builder.Services.AddSingleton<IVectorStore, QdrantVectorStore>();
builder.Services.AddSingleton<IPromptBuilder, PromptBuilder>();

// Register HTTP client for embedding service
builder.Services.AddHttpClient<IEmbeddingService, EmbeddingService>();

var app = builder.Build();

// Configure middleware
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Codebase RAG API v1");
    options.RoutePrefix = "swagger";
});

// Map endpoints
app.MapHealthEndpoints();
app.MapRagEndpoints();

// Redirect root to Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();
