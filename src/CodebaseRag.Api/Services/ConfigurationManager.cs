using System.Text.Json;
using CodebaseRag.Api.Configuration;
using Microsoft.Extensions.Options;

namespace CodebaseRag.Api.Services;

public class ConfigurationManager : IConfigurationManager
{
    private readonly IOptionsMonitor<RagSettings> _settingsMonitor;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ConfigurationManager> _logger;
    private readonly string _overrideFilePath;
    private RagSettings? _overrideSettings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ConfigurationManager(
        IOptionsMonitor<RagSettings> settingsMonitor,
        IWebHostEnvironment environment,
        ILogger<ConfigurationManager> logger)
    {
        _settingsMonitor = settingsMonitor;
        _environment = environment;
        _logger = logger;

        // Store overrides in a separate file
        var settingsDir = Path.Combine(_environment.ContentRootPath, "settings");
        Directory.CreateDirectory(settingsDir);
        _overrideFilePath = Path.Combine(settingsDir, "settings.override.json");

        LoadOverrideSettings();
    }

    public RagSettings GetCurrentSettings()
    {
        // Merge base settings with overrides
        var baseSettings = _settingsMonitor.CurrentValue;

        if (_overrideSettings == null)
            return baseSettings;

        return MergeSettings(baseSettings, _overrideSettings);
    }

    public async Task SaveSettingsAsync(RagSettings settings)
    {
        var (isValid, errors) = ValidateSettings(settings);
        if (!isValid)
        {
            throw new InvalidOperationException($"Invalid settings: {string.Join(", ", errors)}");
        }

        _overrideSettings = settings;

        var wrapper = new { Rag = settings };
        var json = JsonSerializer.Serialize(wrapper, JsonOptions);

        await File.WriteAllTextAsync(_overrideFilePath, json);
        _logger.LogInformation("Settings saved to {Path}", _overrideFilePath);
    }

    public async Task ResetToDefaultsAsync()
    {
        _overrideSettings = null;

        if (File.Exists(_overrideFilePath))
        {
            File.Delete(_overrideFilePath);
            _logger.LogInformation("Override settings deleted");
        }

        await Task.CompletedTask;
    }

    public (bool IsValid, List<string> Errors) ValidateSettings(RagSettings settings)
    {
        var errors = new List<string>();

        // Validate codebase settings
        if (string.IsNullOrWhiteSpace(settings.Codebase.RootPath))
            errors.Add("Codebase root path is required");

        // Validate embedding settings
        if (string.IsNullOrWhiteSpace(settings.Embedding.BaseUrl))
            errors.Add("Embedding base URL is required");

        if (string.IsNullOrWhiteSpace(settings.Embedding.Model))
            errors.Add("Embedding model is required");

        if (settings.Embedding.Dimensions <= 0)
            errors.Add("Embedding dimensions must be positive");

        if (settings.Embedding.BatchSize <= 0 || settings.Embedding.BatchSize > 1000)
            errors.Add("Batch size must be between 1 and 1000");

        // Validate chunking settings
        if (settings.Chunking.MaxChunkSize <= 0)
            errors.Add("Max chunk size must be positive");

        if (settings.Chunking.ChunkOverlap < 0)
            errors.Add("Chunk overlap cannot be negative");

        if (settings.Chunking.ChunkOverlap >= settings.Chunking.MaxChunkSize)
            errors.Add("Chunk overlap must be less than max chunk size");

        // Validate prompt settings
        if (settings.Prompt.MaxContextChunks <= 0)
            errors.Add("Max context chunks must be positive");

        if (settings.Prompt.MaxContextTokens <= 0)
            errors.Add("Max context tokens must be positive");

        return (errors.Count == 0, errors);
    }

    private void LoadOverrideSettings()
    {
        if (!File.Exists(_overrideFilePath))
            return;

        try
        {
            var json = File.ReadAllText(_overrideFilePath);
            var wrapper = JsonSerializer.Deserialize<SettingsWrapper>(json, JsonOptions);
            _overrideSettings = wrapper?.Rag;
            _logger.LogInformation("Loaded override settings from {Path}", _overrideFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load override settings from {Path}", _overrideFilePath);
        }
    }

    private static RagSettings MergeSettings(RagSettings baseSettings, RagSettings overrides)
    {
        // For simplicity, just return the overrides if they exist
        // In production, you might want field-by-field merging
        return overrides;
    }

    private class SettingsWrapper
    {
        public RagSettings? Rag { get; set; }
    }
}
