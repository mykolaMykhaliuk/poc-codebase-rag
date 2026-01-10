# Codebase RAG PoC

A **Proof of Concept** for a "Chat with Code" RAG (Retrieval-Augmented Generation) system. This system indexes source code files, creates semantic embeddings, and generates LLM-ready prompts with relevant code context.

## Features

- **Code Indexing**: Parses C# and JavaScript files into semantic chunks
- **Vector Search**: Uses Qdrant for efficient similarity search
- **LLM-Agnostic**: Returns assembled prompts, no inference performed
- **Admin UI**: Blazor-based web interface for easy management
- **MCP Server**: Built-in Model Context Protocol server for Claude integration
- **Docker-Ready**: Single `docker compose up` deployment
- **Extensible**: Add new parsers via configuration

---

## Quick Setup

This guide will get you up and running in under 5 minutes.

The system includes a **Blazor Admin UI** for easy management - no curl commands required! You can also use the REST API directly for integrations.

---

## Prerequisites

Before starting, ensure you have:

- [ ] **Docker Desktop** installed and running
- [ ] **OpenAI API key** (or compatible embedding API key)
- [ ] **Modern web browser** for the Admin UI (optional: curl for API testing)

### Get an OpenAI API Key

1. Go to https://platform.openai.com/api-keys
2. Click "Create new secret key"
3. Copy the key (starts with `sk-`)

---

## Step 1: Clone the Repository

```bash
git clone https://github.com/mykolaMykhaliuk/poc-codebase-rag.git
cd poc-codebase-rag
```

---

## Step 2: Configure Environment

### Option A: Using .env file (Recommended)

```bash
# Copy the example file
cp .env.example .env

# Edit with your API key
nano .env   # or use any text editor
```

Set your API key in the `.env` file:
```
EMBEDDING_API_KEY=sk-your-actual-api-key-here
CODEBASE_PATH=./sample-codebase
```

### Option B: Using environment variables

```bash
export EMBEDDING_API_KEY="sk-your-actual-api-key-here"
```

---

## Step 3: Start the Services

```bash
docker compose up -d
```

This starts two containers:
- `codebase-rag-api` - The .NET 8 API with Admin UI (port 5000)
- `codebase-rag-qdrant` - Qdrant vector database (port 6333, 6334)

### Verify services are running:

**Option 1: Open the Admin UI**
```
http://localhost:5000
```
If you see the Dashboard, everything is working!

**Option 2: Check with Docker CLI**

```bash
docker compose ps
```

Expected output:
```
NAME                  STATUS
codebase-rag-api      Up (healthy)
codebase-rag-qdrant   Up (healthy)
```

**Option 3: Check health endpoint:**

```bash
curl http://localhost:5000/health
```

Expected response:
```json
{"status":"healthy","version":"1.0.0","timestamp":"2024-..."}
```

---

## Step 4: Build the Index

### Option A: Using curl

Index the sample codebase (or your mounted codebase):

```bash
curl -X POST http://localhost:5000/rag/rebuild
```

Expected response:
```json
{
  "success": true,
  "filesProcessed": 6,
  "chunksIndexed": 45,
  "errors": [],
  "durationMs": 12500
}
```

### Option B: Using Admin UI

1. Open **http://localhost:5000** in your browser
2. Click the **"Rebuild Index"** button on the Dashboard
3. Monitor the progress and view results

> **Note:** First build takes longer as it downloads the embedding model weights.

---

## Step 5: Query Your Codebase

### Option A: Using Admin UI

1. Open **http://localhost:5000** in your browser
2. Navigate to **Query** page from the sidebar
3. Enter your question in the search box
4. View the generated prompt and relevant code snippets
5. See matched files with syntax highlighting

### Option B: Using curl

**Basic Query:**

```bash
curl -X POST http://localhost:5000/rag/query \
  -H "Content-Type: application/json" \
  -d '{"question": "How does user authentication work?"}'
```

**Query with Filters:**

```bash
curl -X POST http://localhost:5000/rag/query \
  -H "Content-Type: application/json" \
  -d '{
    "question": "How do I update product stock?",
    "options": {
      "maxResults": 5,
      "languages": ["csharp"],
      "pathFilter": "src/Services"
    }
  }'
```

### Example Response

```json
{
  "prompt": "[SYSTEM]\nYou are a code assistant...\n\n[CONTEXT]\n---\nFile: src/Services/UserService.cs (lines 25-45)\nLanguage: csharp\nSymbol: UserService.Authenticate (method)\n\n```csharp\npublic User? Authenticate(string username, string password)\n{\n    _logger.LogInformation(\"Authenticating user: {Username}\", username);\n    ...\n}\n```\n\n[QUESTION]\nHow does user authentication work?\n\n[INSTRUCTIONS]\n1. Answer based strictly on the provided code snippets\n...",
  "sources": [
    {
      "filePath": "src/Services/UserService.cs",
      "symbolName": "UserService.Authenticate",
      "lines": "25-45",
      "relevanceScore": 0.92
    }
  ],
  "metadata": {
    "chunksSearched": 45,
    "chunksReturned": 5,
    "embeddingModel": "text-embedding-3-small"
  }
}
```

---

## Step 6: Use with Your LLM

Take the `prompt` field and send it to your preferred LLM:

### With OpenAI (curl)

```bash
# First, get the prompt
PROMPT=$(curl -s -X POST http://localhost:5000/rag/query \
  -H "Content-Type: application/json" \
  -d '{"question": "How does authentication work?"}' | jq -r '.prompt')

# Send to OpenAI
curl https://api.openai.com/v1/chat/completions \
  -H "Authorization: Bearer $OPENAI_API_KEY" \
  -H "Content-Type: application/json" \
  -d "{
    \"model\": \"gpt-4\",
    \"messages\": [{\"role\": \"user\", \"content\": $(echo "$PROMPT" | jq -Rs .)}]
  }"
```

### With Python

```python
import requests

# Get RAG prompt
response = requests.post(
    "http://localhost:5000/rag/query",
    json={"question": "How does authentication work?"}
)
prompt = response.json()["prompt"]

# Send to your LLM
from openai import OpenAI
client = OpenAI()

completion = client.chat.completions.create(
    model="gpt-4",
    messages=[{"role": "user", "content": prompt}]
)
print(completion.choices[0].message.content)
```

---

## Access the Web UIs

### Admin UI (Blazor)

Open in browser: **http://localhost:5000** or **http://localhost:5000/admin**

The Admin UI provides a user-friendly interface to:
- **Dashboard** - View index status, statistics, and recent activity
- **Query Interface** - Test queries with real-time results and code previews
- **Index Status** - Browse indexed files, view statistics by language
- **Settings** - View and manage configuration settings

Perfect for exploring your codebase without writing curl commands!

### Swagger UI

Open in browser: **http://localhost:5000/swagger**

This provides interactive API documentation where you can test endpoints directly.

---

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Basic health check |
| GET | `/health/ready` | Readiness probe (checks dependencies) |
| POST | `/rag/rebuild` | Rebuilds the vector index |
| POST | `/rag/query` | Queries and returns LLM-ready prompt |

---

## MCP Server

This service includes a built-in **Model Context Protocol (MCP)** server, enabling direct integration with Claude Desktop, Claude Code, and other MCP-compatible AI assistants. MCP allows AI assistants to directly query and interact with your indexed codebase.

### MCP Endpoint

The MCP server runs on the same port as the REST API using HTTP/SSE transport:

- **URL**: `http://localhost:5000/mcp`
- **Transport**: Server-Sent Events (SSE)

### Available MCP Tools

| Tool | Description |
|------|-------------|
| `QueryCodebase` | Query the codebase to find relevant code for answering questions |
| `RebuildIndex` | Rebuild the codebase vector index |
| `GetHealth` | Check system health and index status |
| `GetIndexStats` | Get detailed statistics about the indexed codebase |

#### QueryCodebase Tool

Searches the indexed codebase and returns an LLM-ready prompt with relevant code context.

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `question` | string | Yes | The question or query about the codebase |
| `maxResults` | int | No | Maximum code chunks to return (default: 10) |
| `languages` | string[] | No | Filter by language: `csharp`, `javascript`, `plaintext` |
| `pathFilter` | string | No | Filter by file path prefix (e.g., `src/Services`) |

**Returns:** JSON with `prompt`, `sources` (file paths, symbols, line numbers, scores), and `metadata`.

#### RebuildIndex Tool

Scans all source files, generates embeddings, and rebuilds the vector index.

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `force` | bool | No | Force rebuild even if index exists (default: true) |

**Returns:** JSON with `success`, `filesProcessed`, `chunksIndexed`, `errors`, and `durationMs`.

#### GetHealth Tool

Checks the health and connectivity status of the RAG system.

**Returns:** JSON with `status`, `qdrantConnected`, `indexExists`, `chunkCount`, `lastRebuild`, and any errors.

#### GetIndexStats Tool

Provides detailed statistics about the indexed codebase.

**Returns:** JSON with `totalChunks`, `totalFiles`, `filesByLanguage`, `chunksBySymbolType`, and `indexedFiles` list.

### MCP Resources

Resources provide read-only access to system state and can be read by MCP clients.

| Resource URI | Description |
|--------------|-------------|
| `rag://index/status` | Current index status including rebuild time, statistics, and errors |
| `rag://index/files` | List of all indexed files with language and chunk counts |
| `rag://config/settings` | Current RAG configuration (embedding model, chunk sizes, exclusions) |
| `rag://activity/recent` | Recent activity log (queries and index rebuilds) |

### Claude Desktop Integration

Add to your Claude Desktop configuration file:

**macOS:** `~/Library/Application Support/Claude/claude_desktop_config.json`
**Windows:** `%APPDATA%\Claude\claude_desktop_config.json`
**Linux:** `~/.config/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "codebase-rag": {
      "url": "http://localhost:5000/mcp",
      "transport": "sse"
    }
  }
}
```

After saving, restart Claude Desktop. You should see "codebase-rag" listed in your connected MCP servers.

### Claude Code Integration

For Claude Code CLI, add to your MCP settings (`.claude/settings.json` or project-level config):

```json
{
  "mcpServers": {
    "codebase-rag": {
      "type": "sse",
      "url": "http://localhost:5000/mcp"
    }
  }
}
```

### Example MCP Conversations

Once connected, you can interact with your codebase through natural language:

**Basic Queries:**
```
"How does user authentication work in this codebase?"
"Find the code that handles payment processing"
"What database models are defined in this project?"
```

**Filtered Queries:**
```
"Show me all service classes in the src/Services folder"
"How are API endpoints defined? Only look at C# files"
"Find error handling patterns, limit to 5 results"
```

**Index Management:**
```
"Rebuild the codebase index - I just added new files"
"Check the health of the RAG system"
"Show me statistics about what's been indexed"
```

### MCP Troubleshooting

**Connection refused:**
- Ensure Docker containers are running: `docker compose ps`
- Verify the API is healthy: `curl http://localhost:5000/health`

**Tools not appearing:**
- Restart Claude Desktop/Claude Code after config changes
- Check JSON syntax in configuration file
- Verify URL is accessible from your machine

**Empty results:**
- Ensure index was built: use `GetHealth` tool or check `/health/ready`
- Run `RebuildIndex` if the codebase has changed
- Check if files match supported extensions (see Parser Mapping)

**Slow responses:**
- First query may be slow (embedding API call)
- Large codebases take longer to search
- Consider reducing `maxResults` parameter

---

## Index Your Own Codebase

### Option 1: Change CODEBASE_PATH

```bash
# Stop services
docker compose down

# Set new path and restart
CODEBASE_PATH=/path/to/your/code docker compose up -d

# Rebuild index
curl -X POST http://localhost:5000/rag/rebuild
```

### Option 2: Edit docker-compose.yml

```yaml
services:
  api:
    volumes:
      - /path/to/your/code:/codebase:ro  # Change this line
```

Then restart:
```bash
docker compose down && docker compose up -d
curl -X POST http://localhost:5000/rag/rebuild
```

---

## Configuration

Configuration is managed via `appsettings.json` and environment variables.

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `EMBEDDING_API_KEY` | API key for embedding service | (required) |
| `EMBEDDING_BASE_URL` | Embedding API base URL | `https://api.openai.com/v1` |
| `EMBEDDING_MODEL` | Embedding model name | `text-embedding-3-small` |
| `CODEBASE_PATH` | Path to codebase | `./sample-codebase` |
| `MCP_ENABLED` | Enable MCP server | `true` |

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

---

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

---

## Useful Commands

### View Logs

```bash
# All logs
docker compose logs -f

# API logs only
docker compose logs -f api

# Qdrant logs only
docker compose logs -f qdrant
```

### Restart Services

```bash
docker compose restart
```

### Stop Services

```bash
docker compose down
```

### Stop and Remove Data

```bash
docker compose down -v  # Removes Qdrant data volume
```

### Rebuild Docker Image

```bash
docker compose build --no-cache
docker compose up -d
```

---

## Alternative Embedding Providers

### Azure OpenAI

```bash
export EMBEDDING_BASE_URL="https://YOUR-INSTANCE.openai.azure.com/openai/deployments/YOUR-DEPLOYMENT"
export EMBEDDING_API_KEY="your-azure-key"
export EMBEDDING_MODEL="text-embedding-ada-002"
```

### Ollama (Free, Local)

1. Install Ollama: https://ollama.ai
2. Pull embedding model:
   ```bash
   ollama pull nomic-embed-text
   ```
3. Configure:
   ```bash
   export EMBEDDING_BASE_URL="http://host.docker.internal:11434/v1"
   export EMBEDDING_MODEL="nomic-embed-text"
   export EMBEDDING_API_KEY="ollama"  # Required but unused
   ```

---

## Troubleshooting

### "Connection refused" error

```bash
# Check if containers are running
docker compose ps

# If not running, check logs
docker compose logs
```

### "Embedding API error"

```bash
# Verify API key is set
docker compose exec api printenv | grep EMBEDDING

# Check API logs for details
docker compose logs api | grep -i error
```

### "Index is empty" error

```bash
# Rebuild the index
curl -X POST http://localhost:5000/rag/rebuild

# Check for errors in response
```

### Slow index building

This is normal for large codebases. The bottleneck is usually the embedding API rate limit.

Tips:
- Reduce batch size in `appsettings.json`: `"BatchSize": 50`
- Use a local embedding model (Ollama)

### Out of memory

```bash
# Increase Docker memory limit in Docker Desktop settings
# Or limit Qdrant memory in docker-compose.yml:
services:
  qdrant:
    deploy:
      resources:
        limits:
          memory: 2G
```

---

## Quick Reference

| Action | Command |
|--------|---------|
| Start | `docker compose up -d` |
| Stop | `docker compose down` |
| Logs | `docker compose logs -f api` |
| Health | `curl http://localhost:5000/health` |
| Rebuild Index | `curl -X POST http://localhost:5000/rag/rebuild` |
| Query | `curl -X POST http://localhost:5000/rag/query -H "Content-Type: application/json" -d '{"question":"..."}'` |
| Admin UI | http://localhost:5000 |
| Swagger | http://localhost:5000/swagger |

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Docker Compose                         │
├─────────────────────────────────────────────────────────────┤
│  ┌──────────────────────┐    ┌───────────────────────────┐  │
│  │  CodebaseRag.Api     │    │        Qdrant             │  │
│  │  (.NET 8 Web API)    │───▶│    (Vector Database)      │  │
│  │   + Blazor Admin UI  │    │                           │  │
│  │   + MCP Server       │    │                           │  │
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

---

## Project Structure

```
├── src/
│   └── CodebaseRag.Api/
│       ├── Components/       # Blazor Admin UI
│       ├── Configuration/    # Settings classes
│       ├── Contracts/        # Request/response models
│       ├── Endpoints/        # API endpoints
│       ├── Mcp/              # MCP server tools & resources
│       ├── Parsing/          # Code parsers
│       ├── Services/         # Business logic
│       └── Program.cs        # Entry point
├── sample-codebase/          # Example code for testing
├── docs/                     # Architecture documentation
├── docker-compose.yml
├── Dockerfile
└── README.md
```

---

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

### Build Solution

```bash
# Using provided scripts
./build.sh       # Linux/Mac
pwsh build.ps1   # Windows

# Or directly with dotnet
dotnet build CodebaseRag.sln --configuration Release
```

---

## Next Steps

1. **Explore the Admin UI** - Browse indexed files, test queries, and view statistics at http://localhost:5000
2. **Index your codebase** - Point `CODEBASE_PATH` to your project
3. **Integrate with your tools** - Use the API from your IDE, CLI tools, or scripts
4. **Try MCP integration** - Connect with Claude Desktop or Claude Code
5. **Customize settings** - View and adjust settings via Admin UI or edit `appsettings.json`
6. **Add more parsers** - Extend `ParserMapping` for new file types

For architecture details, see [docs/architecture.md](docs/architecture.md).

---

## License

MIT
