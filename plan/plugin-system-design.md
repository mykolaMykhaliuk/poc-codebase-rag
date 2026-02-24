# Plugin System Design for Codebase RAG

## Context

The Codebase RAG system currently has hardcoded parsers (C#, JavaScript, PlainText) and only reads from the local filesystem. This design adds a plugin system to support two extension points:
1. **Parser plugins** - Add new language parsers (e.g., Java, Python) without modifying core code
2. **Data source plugins** - Index code from remote sources (e.g., Azure DevOps, GitHub) alongside local files

The architecture leverages existing seams: `ICodeParser` is already multi-implementation with dynamic routing via `ParserFactory`, and all services are singletons with configuration-driven behavior.

---

## New Files (9)

### Plugin Abstractions (`src/CodebaseRag.Api/Plugins/`)

**1. `PluginType.cs`** - Enum: `Parser`, `DataSource`

**2. `PluginSettingDescriptor.cs`** - Metadata for plugin-configurable settings (key, display name, type, required, sensitive flag). Setting types: `String`, `Number`, `Boolean`, `Password`, `Select`, `MultiSelect`. UI uses this to render correct form controls; `IsSensitive` triggers masking in Admin UI and redaction in API responses.

**3. `PluginState.cs`** - Persistence model for plugin enabled/disabled state and configuration. Status enum: `Discovered`, `Loaded`, `Configured`, `Enabled`, `Disabled`, `Error`. Serialized to `settings/plugins.json` (same pattern as `ConfigurationManager` → `settings/settings.override.json`).

**4. `IPlugin.cs`** - Base interface with `Id`, `Name`, `Description`, `Version`, `Type`, `GetSettingDescriptors()`, `InitializeAsync(config, serviceProvider)`, `ShutdownAsync()`. Receives `IServiceProvider` since plugins are instantiated via `Activator.CreateInstance`, not DI.

**5. `IParserPlugin.cs`** - Extends both `IPlugin` and `ICodeParser`. No additional members needed since `ICodeParser.ParserType` already provides the routing key used by `ParserFactory`, and `ICodeParser.Parse()` is the existing parse contract.

**6. `IDataSourcePlugin.cs`** - Extends `IPlugin`. Methods: `ScanFilesAsync()` returns `DataSourceFile` list (identifier, relative path, extension, source prefix), `GetFileContentAsync(identifier)` fetches content, `TestConnectionAsync()` validates credentials. `DataSourceFile.FileIdentifier` is an opaque string (API URL, blob SHA, etc.) only meaningful to the plugin.

**7. `IPluginManager.cs`** + **`PluginManager.cs`** - Core orchestration service:
  - **Discovery**: Scans `plugins/` directory for DLLs, loads via `AssemblyLoadContext` (collectible), finds `IPlugin` implementations via reflection, restores state from `plugins.json`
  - **Lifecycle**: `EnablePluginAsync` calls `InitializeAsync`, `DisablePluginAsync` calls `ShutdownAsync`
  - **Config**: `UpdateConfigurationAsync` saves config, re-initializes if plugin is active
  - **Resolution**: `GetActiveParserPlugins()` and `GetActiveDataSourcePlugins()` return currently-enabled plugin instances
  - Uses `ConcurrentDictionary` for thread safety (same as `IndexStatusService`)
  - `PluginInfo` DTO for API/UI exposure, `PluginOperationResult` for operation feedback

**8. `PluginStartupService.cs`** - `IHostedService` that runs on app start: calls `DiscoverPluginsAsync()`, then auto-enables plugins that were previously enabled (per `plugins.json` state). Controlled by `RagSettings.Plugins.Enabled` and `AutoEnableOnStartup` flags.

### API & UI

**9. `Endpoints/PluginEndpoints.cs`** - REST API under `/plugins`:
  - `GET /` - List all plugins (redacts sensitive config)
  - `GET /{id}` - Get plugin details
  - `POST /{id}/enable` - Enable plugin
  - `POST /{id}/disable` - Disable plugin
  - `PUT /{id}/config` - Update plugin configuration
  - `POST /discover` - Re-scan plugins directory
  - `POST /{id}/test-connection` - Test data source connection (data source plugins only)

**10. `Components/Pages/Admin/Plugins.razor`** - Admin UI page following existing Blazor patterns from `Settings.razor`:
  - Card grid showing each plugin with name, version, type badge, status
  - Enable/Disable toggle button per plugin
  - Expandable configuration section with form controls derived from `PluginSettingDescriptor`
  - Password fields with show/hide toggle (same pattern as embedding API key in Settings.razor:92-97)
  - "Test Connection" button for data source plugins
  - "Scan for Plugins" button in header

---

## Modified Files (8)

### `Configuration/RagSettings.cs` (line 12)
Add `PluginSettings` property to `RagSettings` class and new `PluginSettings` class:
```csharp
public PluginSettings Plugins { get; set; } = new();
// ...
public class PluginSettings
{
    public string PluginsDirectory { get; set; } = "plugins";
    public bool Enabled { get; set; } = true;
    public bool AutoDiscoverOnStartup { get; set; } = true;
    public bool AutoEnableOnStartup { get; set; } = true;
}
```

### `appsettings.json` (after line 61, inside `"Rag"`)
Add `"Plugins"` section with default values.

### `Program.cs` (after line 48)
Register `IPluginManager` as singleton and `PluginStartupService` as hosted service. After line 82, map `app.MapPluginEndpoints()`.

### `Parsing/ParserFactory.cs`
- Add optional `IPluginManager?` constructor parameter (nullable for backward compat)
- In `GetParser()`: after checking built-in `_parsers` dictionary, fall back to `_pluginManager.GetActiveParserPlugins()` to find a plugin parser matching the requested `parserType`
- Runtime lookup (not constructor-time) so newly enabled plugins work without restart

### `Endpoints/RagEndpoints.cs` (line 39)
- Add `IPluginManager?` parameter to `RebuildIndex` method signature
- After the local file scanning loop (line 92), insert data source plugin loop:
  - Iterate `pluginManager.GetActiveDataSourcePlugins()`
  - For each plugin: call `ScanFilesAsync()`, then for each file call `GetFileContentAsync()` and route through `parserFactory.GetParser()`
  - Prefix file paths with `DataSourceFile.SourcePrefix` for Qdrant storage
  - Wrap in try/catch per-file and per-plugin (matching existing error handling pattern at lines 86-91)

### `Mcp/RagTools.cs` (line 113)
Same changes as `RagEndpoints.cs`: add `IPluginManager?` parameter to `RebuildIndex` MCP tool, insert data source plugin scanning loop after local file processing.

### `Components/Layout/NavMenu.razor` (after line 12)
Add Plugins nav link between Settings and Index Status.

### `Components/Pages/Admin/Settings.razor` (lines 209-213)
- Inject `IPluginManager`
- Replace hardcoded `<select>` parser options with dynamic list that includes plugin parser types appended with "(plugin)" label

### `Components/Pages/Admin/Dashboard.razor` (line 37)
- Inject `IPluginManager`
- Add "Active Plugins" stat card to the stats grid

---

## Implementation Sequence

| Phase | Files | Risk |
|-------|-------|------|
| 1. Abstractions | `PluginType.cs`, `PluginSettingDescriptor.cs`, `PluginState.cs`, `IPlugin.cs`, `IParserPlugin.cs`, `IDataSourcePlugin.cs`, `IPluginManager.cs` | None - no behavioral changes |
| 2. Implementation | `PluginManager.cs`, `PluginStartupService.cs` | None - not wired yet |
| 3. Configuration | `RagSettings.cs`, `appsettings.json`, `Program.cs` (DI registration) | Low - plugin manager starts but finds no plugins |
| 4. Pipeline integration | `ParserFactory.cs`, `RagEndpoints.cs`, `Mcp/RagTools.cs` | Medium - rebuild behavior changes, but behind null check |
| 5. API & UI | `PluginEndpoints.cs`, `Plugins.razor`, `NavMenu.razor`, `Settings.razor`, `Dashboard.razor`, `Program.cs` (endpoint mapping) | Low - additive UI/API |

---

## Example Plugin Implementations (for reference, built as separate classes)

**Java Parser Plugin** (`IParserPlugin`): Regex-based Java parser following `JavaScriptParser` patterns. Regex patterns for `class`, `interface`, `method`, `enum` with brace-matching extraction (same algorithm as `JavaScriptParser.ExtractBlock` at line 93). Falls back to `PlainTextParser` when no structured elements found.

**Azure DevOps Data Source Plugin** (`IDataSourcePlugin`): Uses Azure DevOps REST API with PAT auth. `ScanFilesAsync` calls Items API (recursive), `GetFileContentAsync` fetches individual file content, `TestConnectionAsync` verifies repository access. Settings: org URL, PAT (sensitive), project, repo, branch, include paths.

---

## Verification

1. **Build**: `dotnet build CodebaseRag.sln` should succeed with no errors
2. **Startup**: `docker compose up -d --build` - verify plugin system initializes (logs: "Discovered 0 plugin(s)")
3. **Admin UI**: Navigate to `/admin/plugins` - verify page loads with empty state message
4. **API**: `curl http://localhost:5000/plugins` returns `[]`
5. **API**: `curl -X POST http://localhost:5000/plugins/discover` returns `{"discovered": 0}`
6. **Settings integration**: Verify `/admin/settings` parser dropdown still works with built-in parsers
7. **Rebuild**: `curl -X POST http://localhost:5000/rag/rebuild` still works identically (no plugins installed)
8. **Dashboard**: Verify "Active Plugins: 0" stat card appears on dashboard
