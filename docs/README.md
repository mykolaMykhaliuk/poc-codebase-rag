# Codebase RAG - Documentation

A Retrieval-Augmented Generation (RAG) system for codebase indexing and semantic search.

## Quick Navigation

| Document | Description |
|----------|-------------|
| [Architecture](./architecture.md) | System architecture and component overview |
| [Data Flow](./data-flow.md) | How data moves through the system |
| [Components](./components.md) | Detailed component documentation |

---

## System Overview

```mermaid
graph TB
    subgraph "Docker Environment"
        subgraph "CodebaseRag.Api Container"
            API[REST API<br/>Port 8080]
            UI[Blazor Admin UI<br/>/admin]
            Scanner[Codebase Scanner]
            Parser[Parser Factory]
            Embed[Embedding Service]
            Prompt[Prompt Builder]
        end

        subgraph "Qdrant Container"
            VDB[(Vector Database<br/>Port 6334)]
        end

        VOL[("/codebase<br/>Volume Mount")]
    end

    EXT[External Embedding API<br/>OpenAI / Azure / Ollama]
    USER((User))

    USER -->|HTTP Requests| API
    USER -->|Browser| UI
    API --> Scanner
    Scanner --> VOL
    Scanner --> Parser
    Parser --> Embed
    Embed -->|HTTPS| EXT
    Embed --> VDB
    API --> Prompt
    Prompt --> VDB

    style API fill:#4a9eff,color:#fff
    style UI fill:#9b59b6,color:#fff
    style VDB fill:#27ae60,color:#fff
    style EXT fill:#e74c3c,color:#fff
```

---

## Core Workflow

```mermaid
sequenceDiagram
    participant U as User
    participant A as API
    participant S as Scanner
    participant P as Parser
    participant E as Embedding API
    participant Q as Qdrant

    rect rgb(200, 220, 255)
        Note over U,Q: Index Rebuild Flow
        U->>A: POST /rag/rebuild
        A->>Q: Delete collection
        A->>Q: Create collection
        A->>S: Scan codebase
        S-->>A: File list
        loop Each File
            A->>P: Parse file
            P-->>A: Code chunks
        end
        A->>E: Generate embeddings
        E-->>A: Vectors
        A->>Q: Upsert vectors
        A-->>U: RebuildResponse
    end

    rect rgb(220, 255, 220)
        Note over U,Q: Query Flow
        U->>A: POST /rag/query
        A->>E: Embed question
        E-->>A: Question vector
        A->>Q: Similarity search
        Q-->>A: Matching chunks
        A->>A: Build prompt
        A-->>U: LLM-ready prompt
    end
```

---

## Technology Stack

| Layer | Technology | Purpose |
|-------|------------|---------|
| **Runtime** | .NET 8 | Core framework |
| **API** | ASP.NET Core | REST endpoints |
| **Admin UI** | Blazor Server | Real-time web interface |
| **Parsing** | Roslyn | C# syntax analysis |
| **Vector Store** | Qdrant 1.7.4 | Semantic search |
| **Embedding** | OpenAI API | Text-to-vector conversion |
| **Container** | Docker | Deployment |

---

## Getting Started

```bash
# 1. Clone and configure
cp .env.example .env
# Edit .env with your EMBEDDING_API_KEY

# 2. Start services
docker-compose up -d

# 3. Rebuild index
curl -X POST http://localhost:5000/rag/rebuild

# 4. Query codebase
curl -X POST http://localhost:5000/rag/query \
  -H "Content-Type: application/json" \
  -d '{"question": "How does authentication work?"}'
```

---

## Project Structure

```
poc-codebase-rag/
├── src/CodebaseRag.Api/     # Main application
│   ├── Configuration/       # Settings classes
│   ├── Contracts/           # Request/Response models
│   ├── Endpoints/           # API route handlers
│   ├── Services/            # Core business logic
│   ├── Parsing/             # Code parsers
│   └── Components/          # Blazor UI
├── docs/                    # Documentation
├── sample-codebase/         # Test files
├── docker-compose.yml       # Container orchestration
└── Dockerfile               # Build configuration
```
