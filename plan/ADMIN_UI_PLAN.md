# Admin UI Plan - Codebase RAG

## Overview

Add a simple web-based admin interface to the Codebase RAG system for:
- Viewing and editing RAG configuration settings
- Monitoring index status (file count, chunk count, last rebuild time)
- Triggering index rebuilds
- Testing queries

---

## Technology Choice

### Recommendation: **Blazor Server** (Interactive Server-Side)

| Option | Pros | Cons |
|--------|------|------|
| **Blazor Server** | Simple, no JS, real-time updates, shares backend code | Requires persistent connection |
| Blazor WASM | Runs in browser, offline capable | Larger download, separate project |
| Razor Pages | Simpler, traditional request/response | No real-time, more page reloads |
| React/Vue SPA | Rich ecosystem | Separate codebase, more complexity |

**Decision:** Blazor Server with .NET 8 - keeps everything in one project, C# only, real-time status updates.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      CodebaseRag.Api                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────────┐    ┌─────────────────────────────────┐ │
│  │    REST API         │    │      Blazor Server UI           │ │
│  │    /rag/*           │    │      /admin/*                   │ │
│  │    /health/*        │    │                                 │ │
│  └──────────┬──────────┘    │  ┌───────────┐ ┌─────────────┐  │ │
│             │               │  │ Dashboard │ │  Settings   │  │ │
│             │               │  └───────────┘ └─────────────┘  │ │
│             │               │  ┌───────────┐ ┌─────────────┐  │ │
│             │               │  │  Status   │ │   Query     │  │ │
│             │               │  └───────────┘ └─────────────┘  │ │
│             │               └─────────────────────────────────┘ │
│             │                              │                    │
│             └──────────────┬───────────────┘                    │
│                            ▼                                    │
│                 ┌─────────────────────┐                         │
│                 │  Shared Services    │                         │
│                 │  - IVectorStore     │                         │
│                 │  - IConfigManager   │                         │
│                 │  - IIndexStatus     │                         │
│                 └─────────────────────┘                         │
└─────────────────────────────────────────────────────────────────┘
```

---

## UI Pages

### 1. Dashboard (`/admin`)

Main landing page with overview:

```
┌─────────────────────────────────────────────────────────────────┐
│  🏠 Dashboard                                    [Rebuild Index] │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  │
│  │  📁 Files       │  │  📦 Chunks      │  │  ⏱️ Last Build  │  │
│  │     156         │  │     1,247       │  │  2 hours ago    │  │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘  │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │ Index Status: ✅ Ready                                      ││
│  │ Codebase Path: /codebase                                    ││
│  │ Vector Store: Connected (Qdrant)                            ││
│  │ Embedding Model: text-embedding-3-small                     ││
│  └─────────────────────────────────────────────────────────────┘│
│                                                                 │
│  Recent Activity                                                │
│  ───────────────────────────────────────────────────────────── │
│  • 14:32 - Index rebuilt (156 files, 1247 chunks, 45s)         │
│  • 14:30 - Query: "How does authentication work?"              │
│  • 12:15 - Settings updated: MaxChunkSize → 2000               │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 2. Settings Page (`/admin/settings`)

View and edit all configuration:

```
┌─────────────────────────────────────────────────────────────────┐
│  ⚙️ Settings                                      [Save] [Reset] │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  📂 Codebase                                                    │
│  ├─ Root Path:        [/codebase                    ]           │
│  ├─ Excluded Folders: [bin, obj, node_modules, .git ]           │
│  └─ Excluded Files:   [*.min.js, *.designer.cs      ]           │
│                                                                 │
│  🔗 Embedding                                                   │
│  ├─ Provider:         [OpenAI           ▼]                      │
│  ├─ Base URL:         [https://api.openai.com/v1   ]           │
│  ├─ Model:            [text-embedding-3-small      ]           │
│  ├─ API Key:          [••••••••••••••••] [👁️]                   │
│  └─ Batch Size:       [100    ]                                 │
│                                                                 │
│  ✂️ Chunking                                                    │
│  ├─ Max Chunk Size:   [1500   ] characters                      │
│  ├─ Chunk Overlap:    [200    ] characters                      │
│  └─ Semantic Bounds:  [✓] Prefer semantic boundaries            │
│                                                                 │
│  📝 Prompt                                                      │
│  ├─ Max Context Chunks: [10   ]                                 │
│  ├─ Max Context Tokens: [8000 ]                                 │
│  └─ System Instructions:                                        │
│     ┌─────────────────────────────────────────────────────────┐ │
│     │ You are a code assistant. Answer based ONLY on the     │ │
│     │ provided code snippets...                               │ │
│     └─────────────────────────────────────────────────────────┘ │
│                                                                 │
│  🗂️ Parser Mapping                                    [+ Add]   │
│  ┌──────────┬─────────────┬────────┐                           │
│  │ Extension│ Parser      │ Action │                           │
│  ├──────────┼─────────────┼────────┤                           │
│  │ .cs      │ csharp      │ [🗑️]   │                           │
│  │ .js      │ javascript  │ [🗑️]   │                           │
│  │ .ts      │ javascript  │ [🗑️]   │                           │
│  │ .json    │ plaintext   │ [🗑️]   │                           │
│  └──────────┴─────────────┴────────┘                           │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 3. Index Status Page (`/admin/index`)

Detailed index information:

```
┌─────────────────────────────────────────────────────────────────┐
│  📊 Index Status                          [Rebuild] [Clear All]  │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Status: ✅ Ready                                               │
│  Last Rebuild: 2024-01-15 14:32:05 (2 hours ago)               │
│  Duration: 45.2 seconds                                         │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │              Files by Language                              ││
│  │  ████████████████████░░░░░░ C# (89)                        ││
│  │  ██████████░░░░░░░░░░░░░░░░ JavaScript (42)                ││
│  │  █████░░░░░░░░░░░░░░░░░░░░░ JSON/Config (25)               ││
│  └─────────────────────────────────────────────────────────────┘│
│                                                                 │
│  Files Indexed                                                  │
│  ┌────────────────────────────────┬──────────┬────────┬───────┐│
│  │ File                           │ Language │ Chunks │ Lines ││
│  ├────────────────────────────────┼──────────┼────────┼───────┤│
│  │ src/Services/UserService.cs    │ csharp   │ 12     │ 156   ││
│  │ src/Services/ProductService.cs │ csharp   │ 10     │ 132   ││
│  │ src/Utils/helpers.js           │ javascript│ 8     │ 95    ││
│  │ ...                            │          │        │       ││
│  └────────────────────────────────┴──────────┴────────┴───────┘│
│                                         Showing 1-20 of 156     │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 4. Query Playground (`/admin/query`)

Test queries interactively:

```
┌─────────────────────────────────────────────────────────────────┐
│  🔍 Query Playground                                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Question:                                                      │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │ How does user authentication work?                          ││
│  └─────────────────────────────────────────────────────────────┘│
│                                                                 │
│  Options:                                                       │
│  ├─ Max Results: [10   ]                                        │
│  ├─ Languages:   [✓] C#  [✓] JavaScript  [ ] Plain Text        │
│  └─ Path Filter: [src/Services/*        ]                      │
│                                                                 │
│  [🔍 Search]                                                    │
│                                                                 │
│  ─────────────────────────────────────────────────────────────  │
│                                                                 │
│  Results (5 chunks found in 0.23s)                              │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │ 📄 src/Services/UserService.cs:25-45                        ││
│  │ Symbol: UserService.Authenticate (method)                   ││
│  │ Score: 0.92                                                 ││
│  │ ┌─────────────────────────────────────────────────────────┐ ││
│  │ │ public User? Authenticate(string username, string pass) │ ││
│  │ │ {                                                        │ ││
│  │ │     _logger.LogInformation("Authenticating...");         │ ││
│  │ │     ...                                                  │ ││
│  │ └─────────────────────────────────────────────────────────┘ ││
│  └─────────────────────────────────────────────────────────────┘│
│                                                                 │
│  Generated Prompt:                               [📋 Copy]      │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │ [SYSTEM]                                                    ││
│  │ You are a code assistant...                                 ││
│  │                                                             ││
│  │ [CONTEXT]                                                   ││
│  │ ...                                                         ││
│  └─────────────────────────────────────────────────────────────┘│
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## New Components

### File Structure

```
src/CodebaseRag.Api/
├── Components/                    # Blazor components
│   ├── Layout/
│   │   ├── AdminLayout.razor     # Admin page layout
│   │   └── NavMenu.razor         # Side navigation
│   │
│   ├── Pages/
│   │   ├── Admin/
│   │   │   ├── Dashboard.razor   # Main dashboard
│   │   │   ├── Settings.razor    # Settings editor
│   │   │   ├── IndexStatus.razor # Index details
│   │   │   └── Query.razor       # Query playground
│   │   └── _Imports.razor
│   │
│   ├── Shared/
│   │   ├── StatCard.razor        # Stats display card
│   │   ├── StatusBadge.razor     # Status indicator
│   │   ├── CodeBlock.razor       # Syntax highlighted code
│   │   └── ConfirmDialog.razor   # Confirmation modal
│   │
│   ├── App.razor                 # Root component
│   └── Routes.razor              # Routing config
│
├── Services/
│   ├── IIndexStatusService.cs    # NEW: Index statistics
│   ├── IndexStatusService.cs
│   ├── IConfigurationManager.cs  # NEW: Runtime config management
│   └── ConfigurationManager.cs
│
└── wwwroot/
    ├── css/
    │   └── admin.css             # Admin styles
    └── favicon.ico
```

### New Services

#### 1. IIndexStatusService

Tracks and provides index statistics:

```csharp
public interface IIndexStatusService
{
    IndexStatus GetStatus();
    Task<IndexStatistics> GetStatisticsAsync();
    IEnumerable<IndexedFileInfo> GetIndexedFiles();
    IEnumerable<ActivityLogEntry> GetRecentActivity(int count = 10);
    void RecordActivity(string action, string details);
}

public class IndexStatus
{
    public bool IsReady { get; set; }
    public DateTime? LastRebuildTime { get; set; }
    public TimeSpan? LastRebuildDuration { get; set; }
    public int FilesProcessed { get; set; }
    public int ChunksIndexed { get; set; }
    public List<string> LastErrors { get; set; }
}

public class IndexStatistics
{
    public long TotalChunks { get; set; }
    public Dictionary<string, int> FilesByLanguage { get; set; }
    public Dictionary<string, int> ChunksBySymbolType { get; set; }
}

public class IndexedFileInfo
{
    public string FilePath { get; set; }
    public string Language { get; set; }
    public int ChunkCount { get; set; }
    public int LineCount { get; set; }
    public DateTime IndexedAt { get; set; }
}

public class ActivityLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Action { get; set; }
    public string Details { get; set; }
}
```

#### 2. IConfigurationManager

Runtime configuration management:

```csharp
public interface IConfigurationManager
{
    RagSettings GetCurrentSettings();
    Task SaveSettingsAsync(RagSettings settings);
    Task ResetToDefaultsAsync();
    bool ValidateSettings(RagSettings settings, out List<string> errors);
}
```

---

## Implementation Phases

### Phase 1: Setup Blazor (1-2 hours)

1. Add Blazor Server to existing project
2. Configure routing (`/admin/*`)
3. Create base layout and navigation
4. Add basic CSS styling

**Changes to Program.cs:**
```csharp
// Add Blazor services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add new services
builder.Services.AddSingleton<IIndexStatusService, IndexStatusService>();
builder.Services.AddSingleton<IConfigurationManager, ConfigurationManager>();

// Map Blazor endpoints
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
```

### Phase 2: Dashboard (1-2 hours)

1. Create StatCard component
2. Implement Dashboard page
3. Add real-time status updates
4. Display recent activity log

### Phase 3: Settings Editor (2-3 hours)

1. Create Settings page with form
2. Implement ConfigurationManager service
3. Add validation
4. Persist to appsettings.json (or separate file)
5. Add parser mapping editor

### Phase 4: Index Status (1-2 hours)

1. Create IndexStatus page
2. Implement IndexStatusService
3. Add file listing with pagination
4. Add language distribution chart

### Phase 5: Query Playground (1-2 hours)

1. Create Query page
2. Add query form with options
3. Display results with syntax highlighting
4. Add copy-to-clipboard for prompt

### Phase 6: Polish (1-2 hours)

1. Add loading states
2. Add error handling
3. Improve responsive design
4. Add confirmation dialogs for destructive actions

---

## Settings Persistence Strategy

### Option A: appsettings.override.json (Recommended)

Create a separate override file that takes precedence:

```csharp
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.override.json", optional: true, reloadOnChange: true);
```

**Pros:** Clean separation, survives container restarts (if volume mounted)
**Cons:** Requires file write permissions

### Option B: Database Storage

Store settings in Qdrant or SQLite:

**Pros:** No file permissions needed, versioning possible
**Cons:** More complex, adds dependency

### Option C: Environment Variables + UI (Read-Only Display)

Display current settings but only allow changes via environment variables:

**Pros:** Simplest, follows 12-factor app principles
**Cons:** Can't edit from UI

**Recommendation:** Option A with volume mount for `/app/appsettings.override.json`

---

## Docker Changes

Update docker-compose.yml for settings persistence:

```yaml
services:
  api:
    volumes:
      - ${CODEBASE_PATH:-./sample-codebase}:/codebase:ro
      - ./settings:/app/settings:rw  # NEW: Persist settings
```

---

## Estimated Effort

| Phase | Time |
|-------|------|
| Phase 1: Setup | 1-2 hours |
| Phase 2: Dashboard | 1-2 hours |
| Phase 3: Settings | 2-3 hours |
| Phase 4: Index Status | 1-2 hours |
| Phase 5: Query Playground | 1-2 hours |
| Phase 6: Polish | 1-2 hours |
| **Total** | **8-13 hours** |

---

## Dependencies to Add

```xml
<!-- Already included in .NET 8 Web template -->
<PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="8.0.*" />

<!-- Optional: Better code highlighting -->
<PackageReference Include="Markdig" Version="0.34.*" />
```

---

## Security Considerations

For PoC, the admin UI is open. For production:

1. **Add Authentication:**
   ```csharp
   builder.Services.AddAuthentication("AdminScheme")
       .AddCookie("AdminScheme");

   app.MapRazorComponents<App>()
       .RequireAuthorization();
   ```

2. **Or restrict to localhost:**
   ```csharp
   app.MapRazorComponents<App>()
       .RequireHost("localhost:*");
   ```

---

## Summary

This plan adds a simple Blazor Server admin UI with:

- **Dashboard**: Quick overview of system status
- **Settings**: Edit all RAG configuration
- **Index Status**: Detailed indexing information
- **Query Playground**: Test queries interactively

All in ~8-13 hours of implementation time, staying within the single .NET 8 project.
