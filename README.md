# Codebase RAG PoC

A **Proof of Concept** for a "Chat with Code" RAG (Retrieval-Augmented Generation) system. This system indexes source code files, creates semantic embeddings, and generates LLM-ready prompts with relevant code context.

## Features

- **Code Indexing**: Parses C# and JavaScript files into semantic chunks
- **Vector Search**: Uses Qdrant for efficient similarity search
- **LLM-Agnostic**: Returns assembled prompts, no inference performed
- **Docker-Ready**: Single `docker compose up` deployment
- **Extensible**: Add new parsers via configuration

## Quick Start

### Prerequisites

- Docker and Docker Compose
- OpenAI API key (or compatible embedding API)

### 1. Clone and Configure

```bash
git clone <repository-url>
cd poc-codebase-rag

# Copy and edit environment file
cp .env.example .env
# Edit .env and add your EMBEDDING_API_KEY
```

### 2. Start Services

```bash
# Start with sample codebase
docker compose up -d

# Or specify your own codebase
CODEBASE_PATH=/path/to/your/code docker compose up -d
```

### 3. Build the Index

```bash
curl -X POST http://localhost:5000/rag/rebuild
```

### 4. Query the Codebase

```bash
curl -X POST http://localhost:5000/rag/query \
  -H "Content-Type: application/json" \
  -d '{"question": "How does user authentication work?"}'
```

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Basic health check |
| GET | `/health/ready` | Readiness probe (checks dependencies) |
| POST | `/rag/rebuild` | Rebuilds the vector index |
| POST | `/rag/query` | Queries and returns LLM-ready prompt |

### POST /rag/rebuild

Rebuilds the entire vector index from the mounted codebase.

**Response:**
```json
{
  "success": true,
  "filesProcessed": 6,
  "chunksIndexed": 45,
  "errors": [],
  "durationMs": 12500
}
```

### POST /rag/query

Queries the codebase and returns an assembled prompt.

**Request:**
```json
{
  "question": "How does the ProductService update stock?",
  "options": {
    "maxResults": 10,
    "languages": ["csharp"],
    "pathFilter": "src/Services"
  }
}
```

**Response:**
```json
{
  "prompt": "[SYSTEM]\nYou are a code assistant...\n\n[CONTEXT]\n...\n\n[QUESTION]\nHow does the ProductService update stock?\n\n[INSTRUCTIONS]\n...",
  "sources": [
    {
      "filePath": "src/Services/ProductService.cs",
      "symbolName": "ProductService.UpdateStock",
      "lines": "89-102",
      "relevanceScore": 0.91
    }
  ],
  "metadata": {
    "chunksSearched": 45,
    "chunksReturned": 3,
    "embeddingModel": "text-embedding-3-small"
  }
}
```

## Configuration

Configuration is managed via `appsettings.json` and environment variables.

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `EMBEDDING_API_KEY` | API key for embedding service | (required) |
| `EMBEDDING_BASE_URL` | Embedding API base URL | `https://api.openai.com/v1` |
| `EMBEDDING_MODEL` | Embedding model name | `text-embedding-3-small` |
| `CODEBASE_PATH` | Path to codebase | `./sample-codebase` |

### Parser Mapping

File extensions are mapped to parsers in `appsettings.json`:

```json
{
  "Rag": {
    "ParserMapping": {
      ".cs": "csharp",
      ".js": "javascript",
      ".ts": "javascript",
      ".json": "plaintext"
    }
  }
}
```

Add new extensions without code changes by editing the mapping.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Docker Compose                         │
├─────────────────────────────────────────────────────────────┤
│  ┌──────────────────────┐    ┌───────────────────────────┐  │
│  │  CodebaseRag.Api     │    │        Qdrant             │  │
│  │  (.NET 8 Web API)    │───▶│    (Vector Database)      │  │
│  │                      │    │                           │  │
│  │  Port: 5000          │    │  Port: 6333 (REST)        │  │
│  └──────────┬───────────┘    │  Port: 6334 (gRPC)        │  │
│             │                └───────────────────────────┘  │
│             ▼                                               │
│  ┌──────────────────────┐                                   │
│  │  /codebase (volume)  │                                   │
│  └──────────────────────┘                                   │
└─────────────────────────────────────────────────────────────┘
              │
              ▼ (External)
┌─────────────────────────────────────────────────────────────┐
│              Embedding API (OpenAI, etc.)                   │
└─────────────────────────────────────────────────────────────┘
```

## Parsing Strategy

### C# Files (.cs)
Uses **Roslyn** for accurate AST parsing:
- Classes, structs, interfaces, records
- Methods, properties, constructors
- Nested types supported
- Symbol names and line numbers preserved

### JavaScript Files (.js, .ts)
Uses **regex-based** pattern matching:
- Function declarations
- Arrow functions
- Class declarations
- ES6 exports

### Other Files
Falls back to **plain text chunking** with configurable overlap.

## Project Structure

```
├── src/
│   └── CodebaseRag.Api/
│       ├── Configuration/     # Settings classes
│       ├── Contracts/         # Request/response models
│       ├── Endpoints/         # API endpoints
│       ├── Parsing/           # Code parsers
│       ├── Services/          # Business logic
│       └── Program.cs         # Entry point
├── sample-codebase/           # Example code for testing
├── plan/                      # Architecture documentation
├── docker-compose.yml
├── Dockerfile
└── README.md
```

## Development

### Local Development

```bash
# Restore packages
dotnet restore src/CodebaseRag.Api

# Run locally (requires Qdrant running)
docker run -d -p 6333:6333 -p 6334:6334 qdrant/qdrant:v1.7.4

# Set environment variables
export Rag__Embedding__ApiKey="sk-..."
export Rag__Codebase__RootPath="./sample-codebase"
export Rag__VectorStore__Host="localhost"

# Run the API
dotnet run --project src/CodebaseRag.Api
```

### Swagger UI

Access the Swagger documentation at: `http://localhost:5000/swagger`

## Alternative Embedding Providers

### Azure OpenAI

```bash
export EMBEDDING_BASE_URL="https://{instance}.openai.azure.com"
export EMBEDDING_MODEL="text-embedding-ada-002"
```

### Ollama (Local)

```bash
export EMBEDDING_BASE_URL="http://host.docker.internal:11434/v1"
export EMBEDDING_MODEL="nomic-embed-text"
```

## Troubleshooting

### Index rebuild fails

1. Check embedding API key is set: `echo $EMBEDDING_API_KEY`
2. Verify codebase path exists: `ls $CODEBASE_PATH`
3. Check API logs: `docker compose logs api`

### Query returns no results

1. Ensure index was built: `curl http://localhost:5000/health/ready`
2. Rebuild if needed: `curl -X POST http://localhost:5000/rag/rebuild`

### Qdrant connection issues

1. Check Qdrant is running: `docker compose ps`
2. Verify health: `curl http://localhost:6333/health`

## License

MIT
