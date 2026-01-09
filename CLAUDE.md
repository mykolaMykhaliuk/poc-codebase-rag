# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a **Codebase RAG (Retrieval-Augmented Generation) Proof of Concept** - a "Chat with Code" system that indexes source code files, creates semantic embeddings, and generates LLM-ready prompts with relevant code context. The system is LLM-agnostic and returns assembled prompts without performing inference.

**Tech Stack:**
- ASP.NET Core 8 (Web API)
- Blazor Server (Admin UI)
- Qdrant (vector database)
- Roslyn (C# parsing)
- Docker Compose

## Common Development Commands

### Docker Deployment (Primary)
```bash
# Start services with sample codebase
docker compose up -d

# Start with custom codebase
CODEBASE_PATH=/path/to/your/code docker compose up -d

# View logs
docker compose logs -f api

# Rebuild after code changes
docker compose up -d --build

# Stop services
docker compose down
```

### Local Development
```bash
# Restore dependencies
dotnet restore src/CodebaseRag.Api

# Run locally (requires Qdrant container)
docker run -d -p 6333:6333 -p 6334:6334 qdrant/qdrant:v1.7.4

# Set required environment variables
export Rag__Embedding__ApiKey="sk-..."
export Rag__Codebase__RootPath="./sample-codebase"
export Rag__VectorStore__Host="localhost"

# Run API
dotnet run --project src/CodebaseRag.Api

# Build solution
dotnet build CodebaseRag.sln

# Publish
dotnet publish src/CodebaseRag.Api -c Release -o ./publish
```

### API Operations
```bash
# Rebuild index
curl -X POST http://localhost:5000/rag/rebuild

# Query codebase
curl -X POST http://localhost:5000/rag/query \
  -H "Content-Type: application/json" \
  -d '{"question": "How does authentication work?"}'

# Health checks
curl http://localhost:5000/health
curl http://localhost:5000/health/ready
```

### Access Points
- **Admin UI**: http://localhost:5000/admin (or root `/`)
- **Swagger**: http://localhost:5000/swagger
- **API**: http://localhost:5000
- **Qdrant UI**: http://localhost:6333/dashboard

## Architecture Overview

### Core Pipeline Flow

**Index Rebuild Pipeline:**
1. `RagEndpoints.RebuildIndex()` receives POST to `/rag/rebuild`
2. `QdrantVectorStore` deletes and recreates collection
3. `CodebaseScanner` discovers files (filtered by excludes)
4. `ParserFactory` routes files to appropriate parsers
5. Parsers (`CSharpParser`, `JavaScriptParser`, `PlainTextParser`) extract semantic chunks
6. `EmbeddingService` batches chunks and calls embedding API
7. `QdrantVectorStore` upserts vectors with metadata
8. `IndexStatusService` tracks rebuild metrics

**Query Pipeline:**
1. `RagEndpoints.QueryCodebase()` receives POST to `/rag/query`
2. `EmbeddingService` embeds the question
3. `QdrantVectorStore` performs cosine similarity search
4. `PromptBuilder` assembles LLM-ready prompt with code context
5. Response includes prompt string, sources with file paths/line numbers, and metadata

### Service Layer Architecture

All services are registered as **Singletons** in `Program.cs`:

- **ICodebaseScanner** (`CodebaseScanner`): Discovers and filters source files
- **IParserFactory** (`ParserFactory`): Routes files to parsers based on extension mapping
- **ICodeParser** implementations:
  - `CSharpParser`: Uses Roslyn AST for accurate C# parsing (classes, methods, properties)
  - `JavaScriptParser`: Regex-based parsing for JS/TS (functions, classes, exports)
  - `PlainTextParser`: Fallback sliding window chunking
- **IEmbeddingService** (`EmbeddingService`): Calls external embedding API (OpenAI-compatible)
- **IVectorStore** (`QdrantVectorStore`): Manages Qdrant collections and search
- **IPromptBuilder** (`PromptBuilder`): Formats search results into structured prompts
- **IIndexStatusService** (`IndexStatusService`): Tracks indexing state and activity
- **IConfigurationManager** (`ConfigurationManager`): Runtime configuration management

### Parser Selection Strategy

ParserFactory uses `appsettings.json:Rag.ParserMapping` to route file extensions:
- `.cs` → `CSharpParser` (Roslyn AST)
- `.js/.jsx/.ts/.tsx/.mjs` → `JavaScriptParser` (regex)
- All other supported extensions → `PlainTextParser` (sliding window)

**To add new file types:** Edit `ParserMapping` in `appsettings.json` without code changes.

### Configuration Hierarchy (Priority Order)

1. `appsettings.json` - Default values
2. `appsettings.Development.json` - Environment-specific overrides
3. Environment variables - Docker/runtime (e.g., `Rag__Embedding__ApiKey`)
4. ConfigurationManager - Runtime changes via Admin UI

**Key Settings Sections:**
- `Rag:Codebase` - Root path, excluded folders/files
- `Rag:Embedding` - API URL, key, model, dimensions (1536 for text-embedding-3-small)
- `Rag:Chunking` - Max chunk size (1500), overlap (200)
- `Rag:VectorStore` - Qdrant host/port, collection name
- `Rag:ParserMapping` - Extension → parser type mapping
- `Rag:Prompt` - Max chunks (10), max tokens (8000), system instructions

### Qdrant Vector Store Schema

**Collection:** `codebase_chunks` (configurable)
- **Vector Size:** 1536 dimensions (matches text-embedding-3-small)
- **Distance Metric:** Cosine similarity
- **Payload Fields:**
  - `file_path` - Relative path from codebase root
  - `language` - Parser type (csharp, javascript, plaintext)
  - `symbol_type` - method, class, property, function, etc.
  - `symbol_name` - Fully qualified name (e.g., `ClassName.MethodName`)
  - `content` - Raw code text
  - `start_line` / `end_line` - Line numbers for reference
  - `parent_symbol` - Containing class/namespace
  - `indexed_at` - Timestamp

### Code Chunk Model

The `CodeChunk` class (in `Parsing/CodeChunk.cs`) is the central data structure:
- Created by parsers
- Enriched with embeddings by EmbeddingService
- Stored as Qdrant points
- Returned in search results with relevance scores

## Development Guidelines

### Adding New Language Parsers

1. Create new parser implementing `ICodeParser` interface
2. Set `ParserType` property (e.g., "python")
3. Implement `Parse()` method to return `List<CodeChunk>`
4. Register parser as Singleton in `Program.cs`
5. Add extension mapping in `appsettings.json:Rag.ParserMapping`

**Example:**
```json
{
  "ParserMapping": {
    ".py": "python"
  }
}
```

### Parser Implementation Notes

- **C# Parser** uses `Microsoft.CodeAnalysis.CSharp` (Roslyn) for accurate AST traversal
- **JS Parser** uses regex patterns with brace matching for function/class extraction
- **PlainText Parser** uses sliding window with configurable overlap to avoid splitting logic
- All parsers must populate: FilePath, Content, Language, SymbolType, SymbolName, StartLine, EndLine

### Excluded Files/Folders

Configured in `RagSettings.CodebaseSettings`:
- **Folders:** bin, obj, node_modules, .git, dist, packages, .vs, .idea
- **Patterns:** *.min.js, *.min.css, *.designer.cs, *.g.cs, *.generated.cs

Add more exclusions in `appsettings.json` without code changes.

### Embedding Service Configuration

EmbeddingService is provider-agnostic (OpenAI-compatible API):
- **Batch Size:** 100 chunks per API call (configurable)
- **Timeout:** 60 seconds (configurable)
- **Dimensions:** Must match Qdrant collection size (1536 default)

**Alternative Providers:**
- Azure OpenAI: Set `BaseUrl` to Azure instance
- Ollama (local): Set `BaseUrl` to `http://host.docker.internal:11434/v1`

### Prompt Assembly

PromptBuilder creates structured prompts with sections:
```
[SYSTEM]
<System instructions from config>

[CONTEXT]
---
File: path/file.cs (lines 10-25)
Language: csharp
Symbol: ClassName.Method
```csharp
<code here>
```

[QUESTION]
<User's question>

[INSTRUCTIONS]
<How to answer>
```

Limits:
- MaxContextChunks: 10 (configurable)
- MaxContextTokens: 8000 (configurable)

### Admin UI (Blazor Server)

Located in root components (not in documented structure):
- App.razor - Root component with routing
- AdminLayout.razor - Layout with navigation
- Pages under /admin route:
  - Dashboard: System overview and stats
  - Settings: Runtime configuration
  - IndexStatus: Rebuild history and indexed files
  - Query: Test interface for queries

### Dependency Injection Registration

All services are Singletons (long-lived state):
- Parsers registered individually, consumed by ParserFactory
- EmbeddingService uses IHttpClientFactory for pooled connections
- RagSettings bound from configuration section "Rag"

## Important Implementation Details

### File Path Conventions
- All file paths in chunks are **relative** to `Codebase.RootPath`
- Full paths only used during scanning/reading
- Relative paths stored in Qdrant for portability

### Error Handling in Rebuild
- Individual file parse failures don't stop rebuild
- Errors collected in `RebuildResponse.Errors` list
- Partial success returns 200 with errors listed
- Fatal errors return 500 with error message

### Search Filtering
Query options support:
- `Languages`: Array of parser types (e.g., ["csharp", "javascript"])
- `PathFilter`: Prefix match on file paths (e.g., "src/Services")
- Filters combined with AND logic in Qdrant

### Chunk Deduplication
JavaScriptParser removes duplicate chunks from overlapping regex matches and sorts by line number to maintain proper ordering.

### Health Checks
- `/health`: Basic liveness (always returns 200 if running)
- `/health/ready`: Readiness probe (checks Qdrant connection and collection existence)

### Docker Networking
- API container connects to Qdrant via service name: `qdrant:6334`
- External APIs (embedding) accessed via public internet
- Codebase mounted read-only at `/codebase`

## Key Files to Understand

- `Program.cs` - DI setup, middleware configuration, endpoint mapping
- `Endpoints/RagEndpoints.cs` - Core rebuild and query logic (259 lines)
- `Services/QdrantVectorStore.cs` - Vector DB operations
- `Services/EmbeddingService.cs` - External API calls with batching
- `Parsing/ParserFactory.cs` - Extension → parser routing
- `Configuration/RagSettings.cs` - All configuration models

## Troubleshooting

### Index rebuild fails
- Check embedding API key is set: `echo $EMBEDDING_API_KEY`
- Verify codebase path exists and is mounted correctly
- Check API logs: `docker compose logs api`

### Query returns no results
- Ensure index was built: `curl http://localhost:5000/health/ready`
- Verify chunks were indexed (check rebuild response)
- Try broader query terms

### Qdrant connection issues
- Check Qdrant health: `curl http://localhost:6333/health`
- Verify gRPC port 6334 is accessible
- Check Docker network connectivity

### Parser errors
- CSharpParser requires valid C# syntax (uses Roslyn)
- JavaScriptParser is regex-based and may miss complex patterns
- Falls back to PlainTextParser on parse failures
