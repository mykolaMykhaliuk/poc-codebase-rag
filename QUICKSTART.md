# Quick Setup Guide

This guide will get you up and running with the Codebase RAG system in under 5 minutes.

---

## Prerequisites

Before starting, ensure you have:

- [ ] **Docker Desktop** installed and running
- [ ] **OpenAI API key** (or compatible embedding API key)
- [ ] **curl** or similar HTTP client for testing

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
- `codebase-rag-api` - The .NET 8 API (port 5000)
- `codebase-rag-qdrant` - Qdrant vector database (port 6333, 6334)

### Verify services are running:

```bash
docker compose ps
```

Expected output:
```
NAME                  STATUS
codebase-rag-api      Up (healthy)
codebase-rag-qdrant   Up (healthy)
```

### Check health endpoint:

```bash
curl http://localhost:5000/health
```

Expected response:
```json
{"status":"healthy","version":"1.0.0","timestamp":"2024-..."}
```

---

## Step 4: Build the Index

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

> **Note:** First build takes longer as it downloads the embedding model weights.

---

## Step 5: Query Your Codebase

### Basic Query

```bash
curl -X POST http://localhost:5000/rag/query \
  -H "Content-Type: application/json" \
  -d '{"question": "How does user authentication work?"}'
```

### Query with Filters

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

## Access Swagger UI

Open in browser: **http://localhost:5000/swagger**

This provides interactive API documentation where you can test endpoints directly.

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

## Quick Reference

| Action | Command |
|--------|---------|
| Start | `docker compose up -d` |
| Stop | `docker compose down` |
| Logs | `docker compose logs -f api` |
| Health | `curl http://localhost:5000/health` |
| Rebuild Index | `curl -X POST http://localhost:5000/rag/rebuild` |
| Query | `curl -X POST http://localhost:5000/rag/query -H "Content-Type: application/json" -d '{"question":"..."}'` |
| Swagger | http://localhost:5000/swagger |

---

## Next Steps

1. **Index your codebase** - Point `CODEBASE_PATH` to your project
2. **Integrate with your tools** - Use the API from your IDE, CLI tools, or scripts
3. **Customize settings** - Edit `appsettings.json` for chunking, prompts, etc.
4. **Add more parsers** - Extend `ParserMapping` for new file types

For architecture details, see [plan/ARCHITECTURE.md](plan/ARCHITECTURE.md).
