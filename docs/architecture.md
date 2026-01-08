# Architecture Overview

## Container Architecture

```mermaid
graph TB
    subgraph "Docker Compose Stack"
        subgraph "codebase-rag-api"
            direction TB
            ASPNET["ASP.NET Core 8<br/>Alpine Linux"]
            BLAZOR["Blazor Server"]
            SWAGGER["Swagger UI"]
        end

        subgraph "qdrant"
            direction TB
            QCORE["Qdrant 1.7.4"]
            QREST["REST API :6333"]
            QGRPC["gRPC :6334"]
            QSTOR[("Storage Volume")]
        end

        CODEVOL[("/codebase<br/>Read-Only Mount")]
    end

    ASPNET -->|gRPC| QGRPC
    ASPNET --> CODEVOL
    QCORE --> QSTOR

    EXTAPI["External APIs<br/>OpenAI / Azure / Ollama"]
    ASPNET -->|HTTPS| EXTAPI

    style ASPNET fill:#512bd4,color:#fff
    style QCORE fill:#dc382d,color:#fff
    style EXTAPI fill:#74aa9c,color:#fff
```

---

## Service Layer Architecture

```mermaid
graph LR
    subgraph "API Layer"
        RE[RagEndpoints]
        HE[HealthEndpoints]
    end

    subgraph "Core Services"
        CS[CodebaseScanner]
        ES[EmbeddingService]
        VS[QdrantVectorStore]
        PB[PromptBuilder]
        IS[IndexStatusService]
        CM[ConfigurationManager]
    end

    subgraph "Parsing Layer"
        PF[ParserFactory]
        CSP[CSharpParser]
        JSP[JavaScriptParser]
        PTP[PlainTextParser]
    end

    subgraph "Configuration"
        SETTINGS[RagSettings]
    end

    RE --> CS
    RE --> ES
    RE --> VS
    RE --> PB
    RE --> IS

    CS --> PF
    PF --> CSP
    PF --> JSP
    PF --> PTP

    ES --> SETTINGS
    VS --> SETTINGS
    CS --> SETTINGS
    PB --> SETTINGS

    style RE fill:#3498db,color:#fff
    style HE fill:#3498db,color:#fff
    style VS fill:#27ae60,color:#fff
    style ES fill:#e74c3c,color:#fff
```

---

## Parser Selection Strategy

```mermaid
flowchart TD
    START([File to Parse]) --> EXT{Check Extension}

    EXT -->|.cs| ROSLYN[CSharpParser<br/>Roslyn AST]
    EXT -->|.js .jsx .ts .tsx .mjs| JSPARSE[JavaScriptParser<br/>Regex Patterns]
    EXT -->|Other supported| PLAIN[PlainTextParser<br/>Sliding Window]

    ROSLYN --> RCHECK{Parse<br/>Success?}
    RCHECK -->|Yes| CHUNKS1[Code Chunks<br/>Methods, Classes, Props]
    RCHECK -->|No| PLAIN

    JSPARSE --> JCHECK{Found<br/>Functions?}
    JCHECK -->|Yes| CHUNKS2[Code Chunks<br/>Functions, Classes]
    JCHECK -->|No| PLAIN

    PLAIN --> CHUNKS3[Text Chunks<br/>Overlapping Windows]

    CHUNKS1 --> OUTPUT([CodeChunk Array])
    CHUNKS2 --> OUTPUT
    CHUNKS3 --> OUTPUT

    style ROSLYN fill:#512bd4,color:#fff
    style JSPARSE fill:#f7df1e,color:#000
    style PLAIN fill:#95a5a6,color:#fff
```

---

## Dependency Injection Graph

```mermaid
graph TB
    PROG[Program.cs] --> DI[DI Container]

    DI --> |Singleton| PF[ParserFactory]
    DI --> |Singleton| ES[IEmbeddingService]
    DI --> |Singleton| VS[IVectorStore]
    DI --> |Singleton| CS[ICodebaseScanner]
    DI --> |Singleton| PB[IPromptBuilder]
    DI --> |Singleton| IS[IIndexStatusService]
    DI --> |Singleton| CM[IConfigurationManager]

    PF --> |Creates| CSP[CSharpParser]
    PF --> |Creates| JSP[JavaScriptParser]
    PF --> |Creates| PTP[PlainTextParser]

    DI --> |Configure| SETTINGS[RagSettings]
    DI --> |HttpClient| HTTP[IHttpClientFactory]

    ES --> HTTP
    ES --> SETTINGS
    VS --> SETTINGS

    style PROG fill:#512bd4,color:#fff
    style DI fill:#3498db,color:#fff
```

---

## Vector Database Schema

```mermaid
erDiagram
    COLLECTION ||--o{ POINT : contains

    COLLECTION {
        string name "codebase_chunks"
        int vector_size "1536"
        string distance "Cosine"
    }

    POINT {
        uuid id PK
        float[] vector "1536 dimensions"
    }

    POINT ||--|| PAYLOAD : has

    PAYLOAD {
        string file_path
        string language
        string symbol_type
        string symbol_name
        string content
        int start_line
        int end_line
        string parent_symbol
        datetime indexed_at
    }
```

---

## Blazor Admin UI Structure

```mermaid
graph TB
    subgraph "App.razor"
        ROUTES[Routes.razor]
    end

    subgraph "Layout"
        ADMIN[AdminLayout.razor]
        NAV[NavMenu.razor]
    end

    subgraph "Pages /admin/*"
        DASH[Dashboard.razor<br/>/admin]
        SET[Settings.razor<br/>/admin/settings]
        IDX[IndexStatus.razor<br/>/admin/indexstatus]
        QRY[Query.razor<br/>/admin/query]
    end

    subgraph "Shared Components"
        STAT[StatCard]
        BADGE[StatusBadge]
        SPIN[LoadingSpinner]
    end

    ROUTES --> ADMIN
    ADMIN --> NAV
    ADMIN --> DASH
    ADMIN --> SET
    ADMIN --> IDX
    ADMIN --> QRY

    DASH --> STAT
    DASH --> BADGE
    IDX --> SPIN

    style DASH fill:#9b59b6,color:#fff
    style SET fill:#9b59b6,color:#fff
    style IDX fill:#9b59b6,color:#fff
    style QRY fill:#9b59b6,color:#fff
```

---

## Configuration Hierarchy

```mermaid
flowchart TB
    subgraph "Priority (Low to High)"
        direction TB
        A[appsettings.json<br/>Default values]
        B[appsettings.Development.json<br/>Environment overrides]
        C[Environment Variables<br/>Docker/Runtime]
        D[ConfigurationManager<br/>Runtime changes]
    end

    A --> B --> C --> D

    D --> FINAL[Final Configuration<br/>RagSettings]

    subgraph "RagSettings Sections"
        FINAL --> CB[Codebase<br/>RootPath, Excludes]
        FINAL --> EM[Embedding<br/>API, Model, Key]
        FINAL --> CH[Chunking<br/>Size, Overlap]
        FINAL --> VS[VectorStore<br/>Host, Port]
        FINAL --> PR[Prompt<br/>MaxChunks, Tokens]
        FINAL --> PM[ParserMapping<br/>Extension → Parser]
    end

    style A fill:#bdc3c7
    style B fill:#95a5a6
    style C fill:#7f8c8d
    style D fill:#2c3e50,color:#fff
    style FINAL fill:#27ae60,color:#fff
```

---

## Health Check Flow

```mermaid
sequenceDiagram
    participant C as Client
    participant A as API
    participant Q as Qdrant
    participant F as Filesystem

    C->>A: GET /health
    A-->>C: { status: "healthy", version, timestamp }

    C->>A: GET /health/ready
    A->>Q: Check collection exists
    Q-->>A: Connected
    A->>F: Check /codebase accessible
    F-->>A: Accessible
    A-->>C: { status: "ready", checks: {...} }
```

---

## Docker Build Stages

```mermaid
flowchart LR
    subgraph "Stage 1: Build"
        SDK["dotnet/sdk:8.0"]
        RESTORE[dotnet restore]
        BUILD[dotnet build]
        PUBLISH[dotnet publish]
    end

    subgraph "Stage 2: Runtime"
        ALPINE["aspnet:8.0-alpine"]
        COPY[Copy published files]
        ENTRY[ENTRYPOINT]
    end

    SDK --> RESTORE --> BUILD --> PUBLISH
    PUBLISH --> ALPINE
    ALPINE --> COPY --> ENTRY

    style SDK fill:#512bd4,color:#fff
    style ALPINE fill:#0db7ed,color:#fff
```
