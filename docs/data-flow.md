# Data Flow Documentation

## Index Rebuild Pipeline

The rebuild process transforms source code into searchable vector embeddings.

```mermaid
flowchart TB
    subgraph "1. Initialization"
        REQ[POST /rag/rebuild]
        CLR[Clear existing index]
        CRT[Create new collection]
    end

    subgraph "2. File Discovery"
        SCAN[Scan /codebase]
        FILT[Apply filters]
        FILES[File list]
    end

    subgraph "3. Parsing"
        SELECT[Select parser]
        PARSE[Extract chunks]
        META[Add metadata]
    end

    subgraph "4. Embedding"
        BATCH[Batch chunks]
        API[Call Embedding API]
        VEC[Receive vectors]
    end

    subgraph "5. Storage"
        UPS[Upsert to Qdrant]
        IDX[Index payload]
        DONE[Return response]
    end

    REQ --> CLR --> CRT
    CRT --> SCAN --> FILT --> FILES
    FILES --> SELECT --> PARSE --> META
    META --> BATCH --> API --> VEC
    VEC --> UPS --> IDX --> DONE

    style REQ fill:#3498db,color:#fff
    style API fill:#e74c3c,color:#fff
    style UPS fill:#27ae60,color:#fff
```

---

## Query Pipeline

The query process finds relevant code and builds an LLM-ready prompt.

```mermaid
flowchart TB
    subgraph "1. Request"
        QRY[POST /rag/query]
        VAL[Validate input]
    end

    subgraph "2. Embed Question"
        QEMB[Generate question vector]
        QAPI[Call Embedding API]
    end

    subgraph "3. Search"
        FILT[Build filter]
        SEARCH[Vector similarity search]
        RANK[Rank by score]
    end

    subgraph "4. Prompt Assembly"
        CTX[Format code context]
        SYS[Add system instructions]
        BUILD[Build final prompt]
    end

    subgraph "5. Response"
        SRC[Compile sources]
        RESP[Return QueryResponse]
    end

    QRY --> VAL --> QEMB --> QAPI
    QAPI --> FILT --> SEARCH --> RANK
    RANK --> CTX --> SYS --> BUILD
    BUILD --> SRC --> RESP

    style QRY fill:#3498db,color:#fff
    style QAPI fill:#e74c3c,color:#fff
    style SEARCH fill:#27ae60,color:#fff
    style BUILD fill:#9b59b6,color:#fff
```

---

## Detailed Rebuild Sequence

```mermaid
sequenceDiagram
    participant C as Client
    participant E as RagEndpoints
    participant S as Scanner
    participant P as ParserFactory
    participant EM as EmbeddingService
    participant Q as QdrantVectorStore
    participant API as OpenAI API

    C->>E: POST /rag/rebuild

    Note over E,Q: Phase 1: Setup
    E->>Q: DeleteCollectionAsync()
    Q-->>E: Deleted
    E->>Q: CreateCollectionAsync(1536)
    Q-->>E: Created

    Note over E,S: Phase 2: Scan
    E->>S: ScanAsync()
    S->>S: Enumerate directories
    S->>S: Filter files
    S-->>E: IEnumerable<ScannedFile>

    Note over E,P: Phase 3: Parse
    loop Each File
        E->>P: GetParser(extension)
        P-->>E: ICodeParser
        E->>P: ParseAsync(file)
        P-->>E: List<CodeChunk>
    end

    Note over E,API: Phase 4: Embed
    E->>EM: GenerateEmbeddingsAsync(chunks)
    loop Batches of 100
        EM->>API: POST /embeddings
        API-->>EM: float[][]
    end
    EM-->>E: Chunks with embeddings

    Note over E,Q: Phase 5: Store
    E->>Q: UpsertAsync(chunks)
    Q-->>E: Stored

    E-->>C: RebuildResponse
```

---

## Detailed Query Sequence

```mermaid
sequenceDiagram
    participant C as Client
    participant E as RagEndpoints
    participant EM as EmbeddingService
    participant Q as QdrantVectorStore
    participant PB as PromptBuilder
    participant API as OpenAI API

    C->>E: POST /rag/query<br/>{question, options}

    Note over E: Validate request
    E->>E: Parse QueryRequest

    Note over E,API: Embed question
    E->>EM: GenerateEmbeddingAsync(question)
    EM->>API: POST /embeddings
    API-->>EM: float[1536]
    EM-->>E: questionVector

    Note over E,Q: Search vectors
    E->>Q: SearchAsync(vector, limit, filter)
    Q->>Q: Cosine similarity
    Q-->>E: ScoredChunk[]

    Note over E,PB: Build prompt
    E->>PB: BuildPrompt(question, chunks)
    PB->>PB: Format context
    PB->>PB: Add instructions
    PB-->>E: promptString

    E-->>C: QueryResponse<br/>{prompt, sources, metadata}
```

---

## File Processing Flow

```mermaid
flowchart LR
    subgraph "Input"
        FILE[Source File]
    end

    subgraph "Processing"
        READ[Read content]
        PARSER[Parser]
        CHUNKS[Code Chunks]
    end

    subgraph "Enrichment"
        META[Add metadata]
        EMBED[Generate embedding]
    end

    subgraph "Output"
        POINT[Qdrant Point]
    end

    FILE --> READ --> PARSER --> CHUNKS
    CHUNKS --> META --> EMBED --> POINT

    style FILE fill:#3498db,color:#fff
    style PARSER fill:#9b59b6,color:#fff
    style EMBED fill:#e74c3c,color:#fff
    style POINT fill:#27ae60,color:#fff
```

---

## Chunk Data Model

```mermaid
classDiagram
    class CodeChunk {
        +Guid Id
        +string FilePath
        +string Content
        +string Language
        +string SymbolType
        +string SymbolName
        +int StartLine
        +int EndLine
        +string ParentSymbol
        +float[] Embedding
    }

    class ScannedFile {
        +string FullPath
        +string RelativePath
        +string Extension
    }

    class ScoredChunk {
        +CodeChunk Chunk
        +float Score
    }

    ScannedFile --> CodeChunk : parsed into
    CodeChunk --> ScoredChunk : returned as
```

---

## Embedding Batch Processing

```mermaid
flowchart TB
    INPUT[500 chunks] --> SPLIT[Split into batches]

    SPLIT --> B1[Batch 1<br/>100 chunks]
    SPLIT --> B2[Batch 2<br/>100 chunks]
    SPLIT --> B3[Batch 3<br/>100 chunks]
    SPLIT --> B4[Batch 4<br/>100 chunks]
    SPLIT --> B5[Batch 5<br/>100 chunks]

    B1 --> API1[API Call]
    B2 --> API2[API Call]
    B3 --> API3[API Call]
    B4 --> API4[API Call]
    B5 --> API5[API Call]

    API1 --> V1[Vectors]
    API2 --> V2[Vectors]
    API3 --> V3[Vectors]
    API4 --> V4[Vectors]
    API5 --> V5[Vectors]

    V1 --> MERGE[Merge results]
    V2 --> MERGE
    V3 --> MERGE
    V4 --> MERGE
    V5 --> MERGE

    MERGE --> OUTPUT[500 embedded chunks]

    style INPUT fill:#3498db,color:#fff
    style OUTPUT fill:#27ae60,color:#fff
```

---

## Search Filter Application

```mermaid
flowchart TB
    REQ[Query Request] --> CHECK{Has filters?}

    CHECK -->|No| SEARCH[Search all vectors]
    CHECK -->|Yes| BUILD[Build Qdrant filter]

    BUILD --> LANG{Languages?}
    LANG -->|Yes| LANGF[language IN list]
    LANG -->|No| PATH

    LANGF --> PATH{Path prefix?}
    PATH -->|Yes| PATHF[file_path MATCH prefix*]
    PATH -->|No| COMBINE

    PATHF --> COMBINE[Combine filters]
    COMBINE --> SEARCH

    SEARCH --> RESULTS[Filtered results]

    style REQ fill:#3498db,color:#fff
    style RESULTS fill:#27ae60,color:#fff
```

---

## Prompt Structure

```mermaid
flowchart TB
    subgraph "Prompt Sections"
        direction TB
        SYS["[SYSTEM]<br/>Instructions for the LLM"]
        CTX["[CONTEXT]<br/>Relevant code snippets"]
        QST["[QUESTION]<br/>User's question"]
        INS["[INSTRUCTIONS]<br/>How to answer"]
    end

    SYS --> CTX --> QST --> INS --> FINAL[Complete Prompt]

    subgraph "Context Format"
        direction TB
        C1["---<br/>File: path/file.cs (lines 10-25)<br/>Language: csharp<br/>Symbol: ClassName.Method<br/>```csharp<br/>code here<br/>```"]
        C2["---<br/>File: path/other.cs (lines 5-15)<br/>..."]
    end

    CTX -.-> C1
    CTX -.-> C2

    style FINAL fill:#9b59b6,color:#fff
```

---

## Activity Tracking

```mermaid
stateDiagram-v2
    [*] --> Idle

    Idle --> Rebuilding: POST /rag/rebuild
    Rebuilding --> Ready: Success
    Rebuilding --> Error: Failure

    Ready --> Querying: POST /rag/query
    Querying --> Ready: Response sent

    Ready --> Rebuilding: POST /rag/rebuild
    Error --> Rebuilding: Retry

    state Ready {
        [*] --> Waiting
        Waiting --> Processing: Query received
        Processing --> Waiting: Query complete
    }
```
