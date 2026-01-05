# Implementation Checklist

A step-by-step guide for implementing the Codebase RAG PoC.

---

## Phase 1: Project Setup & Infrastructure

### 1.1 Solution Structure
- [ ] Create solution file `CodebaseRag.sln`
- [ ] Create `src/CodebaseRag.Api` project (.NET 8 Web API)
- [ ] Configure `CodebaseRag.Api.csproj` with required packages
- [ ] Create `.dockerignore` file
- [ ] Create `Dockerfile` (multi-stage build)
- [ ] Create `docker-compose.yml`

### 1.2 Configuration
- [ ] Create `Configuration/RagSettings.cs` (strongly-typed)
- [ ] Create `Configuration/ParserMapping.cs`
- [ ] Configure `appsettings.json` with defaults
- [ ] Create `appsettings.Development.json`
- [ ] Set up environment variable overrides in `Program.cs`

### 1.3 Core Infrastructure
- [ ] Set up minimal API in `Program.cs`
- [ ] Configure dependency injection
- [ ] Add Swagger/OpenAPI configuration
- [ ] Create basic exception middleware

---

## Phase 2: Contracts & Models

### 2.1 Request/Response Models
- [ ] `Contracts/Requests/QueryRequest.cs`
- [ ] `Contracts/Responses/QueryResponse.cs`
- [ ] `Contracts/Responses/RebuildResponse.cs`
- [ ] `Contracts/Responses/HealthResponse.cs`

### 2.2 Domain Models
- [ ] `Parsing/CodeChunk.cs` (chunk model with metadata)

---

## Phase 3: Health Endpoints

### 3.1 Implementation
- [ ] Create `Endpoints/HealthEndpoints.cs`
- [ ] `GET /health` - basic health check
- [ ] `GET /health/ready` - readiness probe (check Qdrant connection)
- [ ] Register endpoints in `Program.cs`

### 3.2 Testing
- [ ] Verify health endpoint returns 200
- [ ] Verify ready endpoint checks Qdrant

---

## Phase 4: Vector Store Integration

### 4.1 Interface & Implementation
- [ ] Create `Services/IVectorStore.cs` interface
- [ ] Create `Services/QdrantVectorStore.cs` implementation
- [ ] Methods: `CreateCollection`, `DeleteCollection`, `Upsert`, `Search`
- [ ] Register in DI container

### 4.2 Docker Verification
- [ ] Start Qdrant container
- [ ] Test connection from API container
- [ ] Verify CRUD operations

---

## Phase 5: Embedding Service

### 5.1 Interface & Implementation
- [ ] Create `Services/IEmbeddingService.cs` interface
- [ ] Create `Services/EmbeddingService.cs` (HTTP client wrapper)
- [ ] Support batch embedding
- [ ] Handle rate limiting and retries
- [ ] Register `HttpClient` with DI

### 5.2 Provider Support
- [ ] OpenAI provider implementation
- [ ] Configuration-based provider selection
- [ ] Error handling for API failures

---

## Phase 6: Parsing Engine

### 6.1 Parser Interface
- [ ] Create `Parsing/ICodeParser.cs` interface
- [ ] Create `Parsing/ParserFactory.cs` (resolves parser by extension)

### 6.2 Plain Text Parser
- [ ] Create `Parsing/PlainTextParser.cs`
- [ ] Implement sliding window chunking
- [ ] Handle configurable overlap

### 6.3 C# Parser (Roslyn)
- [ ] Create `Parsing/CSharpParser.cs`
- [ ] Parse namespaces, classes, methods
- [ ] Extract symbol names and types
- [ ] Handle nested types
- [ ] Track line numbers

### 6.4 JavaScript Parser
- [ ] Create `Parsing/JavaScriptParser.cs`
- [ ] Regex patterns for functions, classes, exports
- [ ] Handle ES6 and CommonJS patterns
- [ ] Fallback to plain text for complex files

### 6.5 Parser Registration
- [ ] Register all parsers in DI
- [ ] Configure parser factory with mapping from settings

---

## Phase 7: Codebase Scanner

### 7.1 Implementation
- [ ] Create `Services/ICodebaseScanner.cs` interface
- [ ] Create `Services/CodebaseScanner.cs` implementation
- [ ] Recursive directory traversal
- [ ] Apply folder exclusions (bin, obj, node_modules, .git)
- [ ] Apply file exclusions (*.min.js, *.designer.cs)
- [ ] Return file list with relative paths

---

## Phase 8: Rebuild Endpoint

### 8.1 Implementation
- [ ] Create `Endpoints/RagEndpoints.cs`
- [ ] `POST /rag/rebuild` endpoint
- [ ] Pipeline: Delete collection → Scan → Parse → Embed → Store
- [ ] Batch processing (configurable batch size)
- [ ] Progress tracking (files, chunks, errors)
- [ ] Return detailed response

### 8.2 Error Handling
- [ ] Continue on single file parse errors
- [ ] Collect and return all errors
- [ ] Proper HTTP status codes (200, 500, 503)

---

## Phase 9: Prompt Builder

### 9.1 Implementation
- [ ] Create `Services/IPromptBuilder.cs` interface
- [ ] Create `Services/PromptBuilder.cs` implementation
- [ ] Template with SYSTEM, CONTEXT, QUESTION, INSTRUCTIONS sections
- [ ] Format code snippets with file paths and line numbers
- [ ] Configurable system instructions
- [ ] Respect max context tokens

---

## Phase 10: Query Endpoint

### 10.1 Implementation
- [ ] `POST /rag/query` endpoint
- [ ] Parse request (question, options)
- [ ] Generate question embedding
- [ ] Vector similarity search
- [ ] Apply optional filters (language, path)
- [ ] Deduplicate overlapping chunks
- [ ] Build prompt via PromptBuilder
- [ ] Return response with sources

### 10.2 Request Validation
- [ ] Validate question is not empty
- [ ] Validate optional filters
- [ ] Return 400 for invalid requests

---

## Phase 11: Polish & Documentation

### 11.1 Error Handling
- [ ] Global exception handler
- [ ] Structured error responses
- [ ] Correlation IDs in logs

### 11.2 Logging
- [ ] Configure structured logging
- [ ] Log indexing progress
- [ ] Log query execution

### 11.3 Sample Codebase
- [ ] Create `/sample-codebase` directory
- [ ] Add sample C# files
- [ ] Add sample JavaScript files
- [ ] Add sample config files

### 11.4 Documentation
- [ ] Update README.md with setup instructions
- [ ] Document API endpoints
- [ ] Document configuration options

---

## Phase 12: Testing

### 12.1 Manual Testing
- [ ] `docker compose up` starts both services
- [ ] Health endpoints respond correctly
- [ ] Rebuild processes sample codebase
- [ ] Query returns relevant results
- [ ] Logs show expected output

### 12.2 Edge Cases
- [ ] Empty codebase
- [ ] Invalid file syntax
- [ ] Very large files
- [ ] Missing embedding API key
- [ ] Qdrant unavailable

---

## File Checklist

```
├── src/
│   └── CodebaseRag.Api/
│       ├── Program.cs                    [ ]
│       ├── CodebaseRag.Api.csproj        [ ]
│       ├── appsettings.json              [ ]
│       ├── appsettings.Development.json  [ ]
│       │
│       ├── Configuration/
│       │   ├── RagSettings.cs            [ ]
│       │   └── ParserMapping.cs          [ ]
│       │
│       ├── Contracts/
│       │   ├── Requests/
│       │   │   └── QueryRequest.cs       [ ]
│       │   └── Responses/
│       │       ├── QueryResponse.cs      [ ]
│       │       ├── RebuildResponse.cs    [ ]
│       │       └── HealthResponse.cs     [ ]
│       │
│       ├── Endpoints/
│       │   ├── RagEndpoints.cs           [ ]
│       │   └── HealthEndpoints.cs        [ ]
│       │
│       ├── Services/
│       │   ├── ICodebaseScanner.cs       [ ]
│       │   ├── CodebaseScanner.cs        [ ]
│       │   ├── IEmbeddingService.cs      [ ]
│       │   ├── EmbeddingService.cs       [ ]
│       │   ├── IVectorStore.cs           [ ]
│       │   ├── QdrantVectorStore.cs      [ ]
│       │   ├── IPromptBuilder.cs         [ ]
│       │   └── PromptBuilder.cs          [ ]
│       │
│       └── Parsing/
│           ├── ICodeParser.cs            [ ]
│           ├── ParserFactory.cs          [ ]
│           ├── CodeChunk.cs              [ ]
│           ├── CSharpParser.cs           [ ]
│           ├── JavaScriptParser.cs       [ ]
│           └── PlainTextParser.cs        [ ]
│
├── sample-codebase/                      [ ]
├── docker-compose.yml                    [ ]
├── Dockerfile                            [ ]
├── .dockerignore                         [ ]
└── README.md                             [ ]
```

---

## Quick Start Commands

```bash
# Clone and navigate
cd poc-codebase-rag

# Set environment variables
export EMBEDDING_API_KEY="sk-your-key"
export CODEBASE_PATH="./sample-codebase"

# Build and start
docker compose up --build -d

# Check health
curl http://localhost:5000/health

# Rebuild index
curl -X POST http://localhost:5000/rag/rebuild

# Query
curl -X POST http://localhost:5000/rag/query \
  -H "Content-Type: application/json" \
  -d '{"question": "What services are available?"}'

# View logs
docker compose logs -f api
```
