namespace CodebaseRag.Api.Configuration;

public class RagSettings
{
    public const string SectionName = "Rag";

    public CodebaseSettings Codebase { get; set; } = new();
    public EmbeddingSettings Embedding { get; set; } = new();
    public ChunkingSettings Chunking { get; set; } = new();
    public VectorStoreSettings VectorStore { get; set; } = new();
    public Dictionary<string, string> ParserMapping { get; set; } = new();
    public PromptSettings Prompt { get; set; } = new();
}

public class CodebaseSettings
{
    public string RootPath { get; set; } = "/codebase";
    public List<string> ExcludedFolders { get; set; } = new()
    {
        "bin", "obj", "node_modules", ".git", "dist", "packages", ".vs", ".idea"
    };
    public List<string> ExcludedFiles { get; set; } = new()
    {
        "*.min.js", "*.min.css", "*.designer.cs", "*.g.cs", "*.generated.cs"
    };
}

public class EmbeddingSettings
{
    public string Provider { get; set; } = "OpenAI";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "text-embedding-3-small";
    public int Dimensions { get; set; } = 1536;
    public int BatchSize { get; set; } = 100;
    public int TimeoutSeconds { get; set; } = 60;
}

public class ChunkingSettings
{
    public int MaxChunkSize { get; set; } = 1500;
    public int ChunkOverlap { get; set; } = 200;
    public bool PreferSemanticBoundaries { get; set; } = true;
}

public class VectorStoreSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6334;
    public string CollectionName { get; set; } = "codebase_chunks";
    public bool UseTls { get; set; } = false;
}

public class PromptSettings
{
    public int MaxContextChunks { get; set; } = 10;
    public int MaxContextTokens { get; set; } = 8000;
    public string SystemInstructions { get; set; } =
        "You are a code assistant. Answer based ONLY on the provided code snippets. " +
        "If the answer cannot be found in the snippets, say so clearly. " +
        "Reference specific file paths and line numbers in your answer.";
}
