using System.Collections.Concurrent;

namespace CodebaseRag.Api.Services;

public class IndexStatusService : IIndexStatusService
{
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<IndexStatusService> _logger;
    private readonly ConcurrentQueue<ActivityLogEntry> _activityLog = new();
    private readonly ConcurrentDictionary<string, IndexedFileInfo> _indexedFiles = new();
    private IndexStatus _currentStatus = new();
    private const int MaxActivityLogSize = 100;

    public IndexStatusService(IVectorStore vectorStore, ILogger<IndexStatusService> logger)
    {
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public IndexStatus GetStatus()
    {
        return _currentStatus;
    }

    public async Task<IndexStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new IndexStatistics();

        try
        {
            stats.TotalChunks = await _vectorStore.GetPointCountAsync(cancellationToken);

            // Aggregate from indexed files
            foreach (var file in _indexedFiles.Values)
            {
                if (!stats.FilesByLanguage.ContainsKey(file.Language))
                    stats.FilesByLanguage[file.Language] = 0;
                stats.FilesByLanguage[file.Language]++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get index statistics");
        }

        return stats;
    }

    public IEnumerable<IndexedFileInfo> GetIndexedFiles()
    {
        return _indexedFiles.Values.OrderBy(f => f.FilePath);
    }

    public IEnumerable<ActivityLogEntry> GetRecentActivity(int count = 20)
    {
        return _activityLog.Reverse().Take(count);
    }

    public void RecordRebuild(int filesProcessed, int chunksIndexed, TimeSpan duration, List<string> errors)
    {
        _currentStatus = new IndexStatus
        {
            IsReady = errors.Count == 0,
            LastRebuildTime = DateTime.UtcNow,
            LastRebuildDuration = duration,
            FilesProcessed = filesProcessed,
            ChunksIndexed = chunksIndexed,
            LastErrors = errors
        };

        AddActivity(new ActivityLogEntry
        {
            Action = "Index Rebuilt",
            Details = $"{filesProcessed} files, {chunksIndexed} chunks in {duration.TotalSeconds:F1}s" +
                     (errors.Count > 0 ? $" ({errors.Count} errors)" : ""),
            Icon = errors.Count > 0 ? "⚠️" : "✅"
        });

        _logger.LogInformation("Recorded rebuild: {Files} files, {Chunks} chunks", filesProcessed, chunksIndexed);
    }

    public void RecordQuery(string question, int resultsCount)
    {
        var truncatedQuestion = question.Length > 50 ? question[..47] + "..." : question;
        AddActivity(new ActivityLogEntry
        {
            Action = "Query",
            Details = $"\"{truncatedQuestion}\" → {resultsCount} results",
            Icon = "🔍"
        });
    }

    public void RecordSettingsChange(string setting, string oldValue, string newValue)
    {
        AddActivity(new ActivityLogEntry
        {
            Action = "Settings Changed",
            Details = $"{setting}: {oldValue} → {newValue}",
            Icon = "⚙️"
        });
    }

    public void RecordIndexedFile(string filePath, string language, int chunkCount)
    {
        _indexedFiles[filePath] = new IndexedFileInfo
        {
            FilePath = filePath,
            Language = language,
            ChunkCount = chunkCount,
            IndexedAt = DateTime.UtcNow
        };
    }

    public void ClearIndexedFiles()
    {
        _indexedFiles.Clear();
    }

    private void AddActivity(ActivityLogEntry entry)
    {
        _activityLog.Enqueue(entry);

        // Trim if too large
        while (_activityLog.Count > MaxActivityLogSize)
        {
            _activityLog.TryDequeue(out _);
        }
    }
}
