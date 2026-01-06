namespace CodebaseRag.Api.Services;

public interface IIndexStatusService
{
    IndexStatus GetStatus();
    Task<IndexStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
    IEnumerable<IndexedFileInfo> GetIndexedFiles();
    IEnumerable<ActivityLogEntry> GetRecentActivity(int count = 20);
    void RecordRebuild(int filesProcessed, int chunksIndexed, TimeSpan duration, List<string> errors);
    void RecordQuery(string question, int resultsCount);
    void RecordSettingsChange(string setting, string oldValue, string newValue);
}

public class IndexStatus
{
    public bool IsReady { get; set; }
    public DateTime? LastRebuildTime { get; set; }
    public TimeSpan? LastRebuildDuration { get; set; }
    public int FilesProcessed { get; set; }
    public int ChunksIndexed { get; set; }
    public List<string> LastErrors { get; set; } = new();
}

public class IndexStatistics
{
    public long TotalChunks { get; set; }
    public Dictionary<string, int> FilesByLanguage { get; set; } = new();
    public Dictionary<string, int> ChunksBySymbolType { get; set; } = new();
}

public class IndexedFileInfo
{
    public required string FilePath { get; set; }
    public required string Language { get; set; }
    public int ChunkCount { get; set; }
    public DateTime IndexedAt { get; set; }
}

public class ActivityLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public required string Action { get; set; }
    public required string Details { get; set; }
    public string Icon { get; set; } = "📝";
}
