# Codebase RAG PoC - Architecture Plan

## Executive Summary

This document outlines the architecture for a **Proof of Concept "Chat with Code" RAG system**. The system indexes source code files, creates semantic embeddings, and generates LLM-ready prompts with relevant code context.

**Key Design Principles:**
- LLM-agnostic: No inference performed, only prompt assembly
- Minimal footprint: 2 Docker containers maximum
- Extensible: Parser mapping via configuration
- Deterministic: Reproducible indexing and retrieval

---

## 1. System Architecture

### 1.1 High-Level Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        Docker Compose                           │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────────────────┐    ┌─────────────────────────────┐ │
│  │    CodebaseRag.Api      │    │         Qdrant              │ │
│  │    (.NET 8 Web API)     │───▶│    (Vector Database)        │ │
│  │                         │    │                             │ │
│  │  - Parsing Engine       │    │  - Collections              │ │
│  │  - Embedding Client     │    │  - Vector Search            │ │
│  │  - Prompt Builder       │    │  - Metadata Filtering       │ │
│  └───────────┬─────────────┘    └─────────────────────────────┘ │
│              │                                                   │
│              ▼                                                   │
│  ┌─────────────────────────┐                                    │
│  │   /codebase (volume)    │                                    │
│  │   Mounted source code   │                                    │
│  └─────────────────────────┘                                    │
└─────────────────────────────────────────────────────────────────┘
                │
                ▼ (External - NOT part of system)
┌─────────────────────────────────────────────────────────────────┐
│              External Embedding API                              │
│         (OpenAI, Azure OpenAI, Ollama, etc.)                    │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 Component Responsibilities

| Component | Responsibility |
|-----------|----------------|
| **CodebaseRag.Api** | REST API, file scanning, parsing, embedding orchestration, prompt assembly |
| **Qdrant** | Vector storage, similarity search, metadata filtering |
| **Codebase Volume** | Read-only mount of source code to index |
| **Embedding API** | External service for generating text embeddings (not containerized) |

### 1.3 Why Qdrant?

| Criteria | Qdrant | Postgres+pgvector |
|----------|--------|-------------------|
| Setup complexity | Single container, zero config | Requires extension installation |
| Vector-first design | Native | Bolted-on |
| Filtering performance | Optimized | General-purpose |
| Memory efficiency | Excellent for vectors | Higher overhead |
| PoC suitability | ★★★★★ | ★★★☆☆ |

**Decision:** Use **Qdrant** for simplicity and vector-native operations.

---

## 2. Project Structure

```
/src
├── CodebaseRag.Api/
│   ├── Program.cs                    # Minimal API entry point
│   ├── CodebaseRag.Api.csproj
│   │
│   ├── Configuration/
│   │   ├── RagSettings.cs            # Strongly-typed settings
│   │   └── ParserMapping.cs          # Extension → parser mapping
│   │
│   ├── Contracts/
│   │   ├── Requests/
│   │   │   └── QueryRequest.cs
│   │   └── Responses/
│   │       ├── QueryResponse.cs
│   │       ├── RebuildResponse.cs
│   │       └── HealthResponse.cs
│   │
│   ├── Endpoints/
│   │   ├── RagEndpoints.cs           # /rag/rebuild, /rag/query
│   │   └── HealthEndpoints.cs        # /health
│   │
│   ├── Services/
│   │   ├── ICodebaseScanner.cs
│   │   ├── CodebaseScanner.cs        # Recursive file discovery
│   │   ├── IEmbeddingService.cs
│   │   ├── EmbeddingService.cs       # External API client
│   │   ├── IVectorStore.cs
│   │   ├── QdrantVectorStore.cs      # Qdrant client wrapper
│   │   ├── IPromptBuilder.cs
│   │   └── PromptBuilder.cs          # Assembles LLM prompts
│   │
│   ├── Parsing/
│   │   ├── ICodeParser.cs            # Parser interface
│   │   ├── ParserFactory.cs          # Resolves parser by extension
│   │   ├── CodeChunk.cs              # Chunk model
│   │   ├── CSharpParser.cs           # Roslyn-based C# parser
│   │   ├── JavaScriptParser.cs       # JS parser (regex-based)
│   │   └── PlainTextParser.cs        # Fallback chunker
│   │
│   └── appsettings.json              # Default configuration
│
├── CodebaseRag.Api.Tests/            # Unit tests (optional for PoC)
│
├── docker-compose.yml
├── Dockerfile
└── .dockerignore
```

---

## 3. Configuration Design

### 3.1 Settings Schema (appsettings.json)

```json
{
  "Rag": {
    "Codebase": {
      "RootPath": "/codebase",
      "ExcludedFolders": ["bin", "obj", "node_modules", ".git", "dist", "packages"],
      "ExcludedFiles": ["*.min.js", "*.designer.cs", "*.g.cs"]
    },
    "Embedding": {
      "Provider": "OpenAI",
      "BaseUrl": "https://api.openai.com/v1",
      "ApiKey": "${EMBEDDING_API_KEY}",
      "Model": "text-embedding-3-small",
      "Dimensions": 1536,
      "BatchSize": 100
    },
    "Chunking": {
      "MaxChunkSize": 1500,
      "ChunkOverlap": 200,
      "PreferSemanticBoundaries": true
    },
    "VectorStore": {
      "Host": "qdrant",
      "Port": 6334,
      "CollectionName": "codebase_chunks",
      "UseTls": false
    },
    "ParserMapping": {
      ".cs": "csharp",
      ".csx": "csharp",
      ".js": "javascript",
      ".jsx": "javascript",
      ".ts": "javascript",
      ".tsx": "javascript",
      ".json": "plaintext",
      ".xml": "plaintext",
      ".yaml": "plaintext",
      ".yml": "plaintext",
      ".md": "plaintext",
      ".txt": "plaintext",
      ".sql": "plaintext",
      ".html": "plaintext",
      ".css": "plaintext"
    },
    "Prompt": {
      "MaxContextChunks": 10,
      "MaxContextTokens": 8000,
      "SystemInstructions": "You are a code assistant. Answer based ONLY on the provided code snippets. If the answer cannot be found in the snippets, say so clearly."
    }
  }
}
```

### 3.2 Environment Variable Override

Configuration values can be overridden via environment variables:
- `Rag__Embedding__ApiKey` → API key for embedding service
- `Rag__Codebase__RootPath` → Path to mounted codebase
- `Rag__VectorStore__Host` → Qdrant hostname

### 3.3 Parser Mapping Extensibility

The `ParserMapping` section allows adding new file types without code changes:

```json
{
  "ParserMapping": {
    ".py": "plaintext",      // Add Python support (falls back to plain)
    ".go": "plaintext",      // Add Go support
    ".custom": "plaintext"   // Any custom extension
  }
}
```

**Future Extension Point:** New parser implementations can be added via:
1. Implement `ICodeParser`
2. Register in `ParserFactory`
3. Map extension in configuration

---

## 4. Parsing Strategy

### 4.1 Parser Interface

```csharp
public interface ICodeParser
{
    string ParserType { get; }
    IEnumerable<CodeChunk> Parse(string filePath, string content, ChunkingSettings settings);
}
```

### 4.2 C# Parser (Roslyn-Based)

**Strategy:** Use Roslyn Syntax API for semantic chunking.

```
Input: MyService.cs
       ├── Namespace: MyApp.Services
       │   └── Class: MyService
       │       ├── Method: GetUser()
       │       ├── Method: SaveUser()
       │       └── Property: ConnectionString

Output Chunks:
  [1] Namespace + Class declaration header
  [2] GetUser() method (full body)
  [3] SaveUser() method (full body)
  [4] Properties block
```

**Chunking Rules:**
1. Each method/property → separate chunk (if under `MaxChunkSize`)
2. Large methods → split at logical boundaries (braces, blank lines)
3. Class-level metadata prepended to each chunk for context
4. Nested classes handled recursively

### 4.3 JavaScript Parser (Regex + Heuristics)

**Strategy:** Pattern-based extraction (no full AST for PoC simplicity).

**Patterns Detected:**
```javascript
// Function declarations
function functionName(params) { ... }

// Arrow functions assigned to const/let
const functionName = (params) => { ... }

// Class declarations
class ClassName { ... }

// ES6 exports
export function/class/const ...
export default ...

// CommonJS
module.exports = ...
```

**Chunking Rules:**
1. Each function/class → separate chunk
2. Export blocks kept together
3. Large files without clear boundaries → sliding window

### 4.4 Plain Text Parser (Fallback)

**Strategy:** Sliding window with overlap.

```
Input: config.json (2000 chars)
Settings: MaxChunkSize=1500, Overlap=200

Output:
  [1] chars 0-1500
  [2] chars 1300-2000 (200 char overlap)
```

### 4.5 Chunk Metadata Model

```csharp
public class CodeChunk
{
    public string Id { get; set; }              // GUID
    public string FilePath { get; set; }        // Relative path from root
    public string Language { get; set; }        // "csharp", "javascript", "plaintext"
    public string SymbolType { get; set; }      // "class", "method", "function", "unknown"
    public string SymbolName { get; set; }      // "MyService.GetUser" or null
    public string Content { get; set; }         // Actual code text
    public int StartLine { get; set; }          // Line number in original file
    public int EndLine { get; set; }
    public string ParentSymbol { get; set; }    // Containing class/namespace
    public DateTime IndexedAt { get; set; }
}
```

---

## 5. Indexing Pipeline

### 5.1 Rebuild Process Flow

```
POST /rag/rebuild
        │
        ▼
┌───────────────────┐
│ Delete existing   │
│ collection        │
└───────┬───────────┘
        ▼
┌───────────────────┐
│ Create new        │
│ collection        │
└───────┬───────────┘
        ▼
┌───────────────────┐
│ Scan codebase     │──── Applies exclusion filters
│ recursively       │
└───────┬───────────┘
        ▼
┌───────────────────┐
│ For each file:    │
│ - Select parser   │
│ - Parse to chunks │
└───────┬───────────┘
        ▼
┌───────────────────┐
│ Batch chunks      │──── Groups of 100
│ (configurable)    │
└───────┬───────────┘
        ▼
┌───────────────────┐
│ Generate          │──── External API call
│ embeddings        │
└───────┬───────────┘
        ▼
┌───────────────────┐
│ Upsert to         │
│ Qdrant            │
└───────┬───────────┘
        ▼
    Response: {
      "success": true,
      "filesProcessed": 150,
      "chunksIndexed": 1200,
      "durationMs": 45000
    }
```

### 5.2 Incremental Index (Future Enhancement)

For PoC, full rebuild is sufficient. Future versions could:
- Hash files to detect changes
- Only re-index modified files
- Support file watchers for real-time updates

---

## 6. Query Pipeline

### 6.1 Query Process Flow

```
POST /rag/query
  { "question": "How does UserService authenticate?" }
        │
        ▼
┌───────────────────┐
│ Embed question    │──── Same model as indexing
│                   │
└───────┬───────────┘
        ▼
┌───────────────────┐
│ Vector similarity │
│ search in Qdrant  │──── Top K results (configurable)
└───────┬───────────┘
        ▼
┌───────────────────┐
│ Optional: filter  │──── By language, path, symbol type
│ metadata          │
└───────┬───────────┘
        ▼
┌───────────────────┐
│ Rank & dedupe     │──── Remove overlapping chunks
│ results           │
└───────┬───────────┘
        ▼
┌───────────────────┐
│ Assemble prompt   │──── PromptBuilder
│                   │
└───────┬───────────┘
        ▼
    Response: {
      "prompt": "...",
      "sources": [...]
    }
```

### 6.2 Prompt Assembly Template

```
[SYSTEM]
{SystemInstructions from config}

[CONTEXT]
The following code snippets are from the codebase and are relevant to your question.
Use ONLY this information to answer. Do not make assumptions beyond what is shown.

---
File: {FilePath1} (lines {StartLine}-{EndLine})
Language: {Language}
Symbol: {SymbolName}

```{language}
{Content}
```

---
File: {FilePath2} ...
...

[QUESTION]
{OriginalUserQuestion}

[INSTRUCTIONS]
1. Answer based strictly on the provided code snippets
2. Reference specific file paths and line numbers in your answer
3. If the code doesn't contain enough information, state that clearly
4. Do not invent or assume code that isn't shown
```

---

## 7. API Contract

### 7.1 Endpoints Summary

| Method | Path | Description |
|--------|------|-------------|
| POST | `/rag/rebuild` | Rebuilds the entire index |
| POST | `/rag/query` | Generates LLM prompt from query |
| GET | `/health` | Health check |
| GET | `/health/ready` | Readiness probe (DB connected) |

### 7.2 POST /rag/rebuild

**Request:** No body required

**Response (200 OK):**
```json
{
  "success": true,
  "filesProcessed": 150,
  "chunksIndexed": 1247,
  "errors": [],
  "durationMs": 42350
}
```

**Response (500 Error):**
```json
{
  "success": false,
  "filesProcessed": 45,
  "chunksIndexed": 312,
  "errors": [
    "Failed to parse: /codebase/broken.cs - Syntax error at line 45"
  ],
  "durationMs": 15000
}
```

### 7.3 POST /rag/query

**Request:**
```json
{
  "question": "How does the UserService authenticate users?",
  "options": {
    "maxResults": 10,
    "languages": ["csharp"],
    "pathFilter": "src/Services/*"
  }
}
```

**Response (200 OK):**
```json
{
  "prompt": "You are a code assistant...\n\n[CONTEXT]\n...\n\n[QUESTION]\nHow does the UserService authenticate users?\n\n[INSTRUCTIONS]\n...",
  "sources": [
    {
      "filePath": "src/Services/UserService.cs",
      "symbolName": "UserService.Authenticate",
      "lines": "45-78",
      "relevanceScore": 0.92
    },
    {
      "filePath": "src/Services/AuthHelper.cs",
      "symbolName": "AuthHelper.ValidateToken",
      "lines": "12-35",
      "relevanceScore": 0.87
    }
  ],
  "metadata": {
    "chunksSearched": 1247,
    "chunksReturned": 10,
    "embeddingModel": "text-embedding-3-small"
  }
}
```

### 7.4 GET /health

**Response (200 OK):**
```json
{
  "status": "healthy",
  "version": "1.0.0",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### 7.5 GET /health/ready

**Response (200 OK):**
```json
{
  "status": "ready",
  "checks": {
    "vectorStore": "connected",
    "codebasePath": "accessible"
  }
}
```

---

## 8. Docker Configuration

### 8.1 docker-compose.yml

```yaml
version: '3.8'

services:
  api:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: codebase-rag-api
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Rag__Embedding__ApiKey=${EMBEDDING_API_KEY}
      - Rag__VectorStore__Host=qdrant
    volumes:
      - ${CODEBASE_PATH:-./sample-codebase}:/codebase:ro
      - ./appsettings.Production.json:/app/appsettings.Production.json:ro
    depends_on:
      qdrant:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    restart: unless-stopped

  qdrant:
    image: qdrant/qdrant:v1.7.4
    container_name: codebase-rag-qdrant
    ports:
      - "6333:6333"   # REST API
      - "6334:6334"   # gRPC
    volumes:
      - qdrant_data:/qdrant/storage
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:6333/health"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped

volumes:
  qdrant_data:
```

### 8.2 Dockerfile (Multi-Stage)

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/CodebaseRag.Api/CodebaseRag.Api.csproj", "CodebaseRag.Api/"]
RUN dotnet restore "CodebaseRag.Api/CodebaseRag.Api.csproj"

COPY src/ .
WORKDIR /src/CodebaseRag.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Install curl for health checks
RUN apk add --no-cache curl

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CodebaseRag.Api.dll"]
```

### 8.3 Usage

```bash
# Set required environment variables
export EMBEDDING_API_KEY="sk-..."
export CODEBASE_PATH="/path/to/your/codebase"

# Start services
docker compose up -d

# Check logs
docker compose logs -f api

# Rebuild index
curl -X POST http://localhost:5000/rag/rebuild

# Query
curl -X POST http://localhost:5000/rag/query \
  -H "Content-Type: application/json" \
  -d '{"question": "How does authentication work?"}'
```

---

## 9. Key Dependencies

### 9.1 NuGet Packages

```xml
<ItemGroup>
  <!-- ASP.NET Core -->
  <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="8.0.*" />
  <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.*" />

  <!-- Roslyn (C# parsing) -->
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.*" />

  <!-- Qdrant client -->
  <PackageReference Include="Qdrant.Client" Version="1.7.*" />

  <!-- HTTP client for embedding API -->
  <PackageReference Include="Microsoft.Extensions.Http" Version="8.0.*" />

  <!-- JSON handling -->
  <PackageReference Include="System.Text.Json" Version="8.0.*" />
</ItemGroup>
```

### 9.2 External Dependencies

| Dependency | Purpose | Required |
|------------|---------|----------|
| Embedding API | Generate vectors | Yes |
| Docker | Container runtime | Yes |
| .NET 8 SDK | Build (local dev) | Dev only |

---

## 10. Error Handling Strategy

### 10.1 Failure Modes

| Scenario | Behavior |
|----------|----------|
| Embedding API unavailable | Return 503 with retry-after header |
| Qdrant unavailable | Return 503, log error |
| Parse error on single file | Log warning, continue with other files |
| Invalid query | Return 400 with validation details |
| Empty codebase | Return 200 with warning |

### 10.2 Logging

Use structured logging with correlation IDs:

```csharp
logger.LogInformation(
    "Processing file {FilePath}, parser={Parser}, chunks={ChunkCount}",
    filePath, parserType, chunks.Count);
```

---

## 11. Security Considerations

### 11.1 PoC Scope

For the PoC, security is minimal but considered:

| Aspect | PoC Approach |
|--------|--------------|
| API Authentication | None (local use) |
| Codebase access | Read-only mount |
| API key storage | Environment variable |
| Network | Internal Docker network |

### 11.2 Production Recommendations

- Add API key authentication to endpoints
- Enable TLS for Qdrant connection
- Use Docker secrets for sensitive config
- Add rate limiting to prevent abuse
- Audit log all rebuild operations

---

## 12. Testing Strategy

### 12.1 Manual Testing Checklist

- [ ] Health endpoint responds
- [ ] Rebuild with empty codebase
- [ ] Rebuild with C# files
- [ ] Rebuild with JS files
- [ ] Rebuild with mixed files
- [ ] Query returns relevant results
- [ ] Query with filters works
- [ ] Invalid query returns 400

### 12.2 Sample Test Files

Include in `/sample-codebase`:
```
sample-codebase/
├── src/
│   ├── Services/
│   │   ├── UserService.cs
│   │   └── ProductService.cs
│   ├── Models/
│   │   └── User.cs
│   └── Utils/
│       └── helpers.js
└── config/
    └── settings.json
```

---

## 13. Implementation Phases

### Phase 1: Foundation
1. Project scaffolding and configuration
2. Health endpoints
3. Qdrant integration
4. Docker setup

### Phase 2: Parsing
1. Plain text parser
2. C# parser (Roslyn)
3. JavaScript parser
4. Parser factory

### Phase 3: Indexing
1. Codebase scanner
2. Embedding service client
3. Rebuild endpoint
4. Batch processing

### Phase 4: Query
1. Vector search
2. Prompt builder
3. Query endpoint
4. Response formatting

### Phase 5: Polish
1. Error handling
2. Logging
3. Documentation
4. Sample codebase

---

## 14. Appendix

### A. Alternative Embedding Providers

The system supports multiple providers via configuration:

**OpenAI:**
```json
{
  "Provider": "OpenAI",
  "BaseUrl": "https://api.openai.com/v1",
  "Model": "text-embedding-3-small"
}
```

**Azure OpenAI:**
```json
{
  "Provider": "AzureOpenAI",
  "BaseUrl": "https://{instance}.openai.azure.com",
  "DeploymentName": "text-embedding-ada-002"
}
```

**Ollama (local):**
```json
{
  "Provider": "Ollama",
  "BaseUrl": "http://host.docker.internal:11434",
  "Model": "nomic-embed-text"
}
```

### B. Performance Estimates

| Codebase Size | Files | Est. Chunks | Indexing Time* |
|---------------|-------|-------------|----------------|
| Small | 50 | 200 | ~30s |
| Medium | 500 | 2,000 | ~5min |
| Large | 5,000 | 20,000 | ~45min |

*Depends on embedding API rate limits and latency

### C. Glossary

| Term | Definition |
|------|------------|
| Chunk | A discrete unit of code indexed separately |
| Embedding | Vector representation of text |
| RAG | Retrieval-Augmented Generation |
| Vector Store | Database optimized for similarity search |
