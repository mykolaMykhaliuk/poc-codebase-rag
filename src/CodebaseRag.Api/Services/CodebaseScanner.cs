using CodebaseRag.Api.Configuration;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Options;

namespace CodebaseRag.Api.Services;

public class CodebaseScanner : ICodebaseScanner
{
    private readonly CodebaseSettings _settings;
    private readonly Dictionary<string, string> _parserMapping;
    private readonly ILogger<CodebaseScanner> _logger;

    public CodebaseScanner(IOptions<RagSettings> settings, ILogger<CodebaseScanner> logger)
    {
        _settings = settings.Value.Codebase;
        _parserMapping = settings.Value.ParserMapping;
        _logger = logger;
    }

    public IEnumerable<ScannedFile> ScanFiles()
    {
        var rootPath = _settings.RootPath;

        if (!Directory.Exists(rootPath))
        {
            _logger.LogWarning("Codebase root path does not exist: {RootPath}", rootPath);
            yield break;
        }

        var excludedFolders = new HashSet<string>(_settings.ExcludedFolders, StringComparer.OrdinalIgnoreCase);
        var excludedPatterns = _settings.ExcludedFiles;
        var supportedExtensions = new HashSet<string>(_parserMapping.Keys, StringComparer.OrdinalIgnoreCase);

        var matcher = new Matcher();
        foreach (var pattern in excludedPatterns)
        {
            matcher.AddInclude(pattern);
        }

        _logger.LogInformation("Scanning codebase at {RootPath}", rootPath);
        var fileCount = 0;

        foreach (var file in EnumerateFiles(rootPath, excludedFolders))
        {
            var extension = Path.GetExtension(file);

            // Skip if extension not in parser mapping
            if (!supportedExtensions.Contains(extension))
                continue;

            var relativePath = Path.GetRelativePath(rootPath, file);

            // Check if file matches exclusion patterns
            if (IsExcluded(Path.GetFileName(file), excludedPatterns))
                continue;

            fileCount++;
            yield return new ScannedFile
            {
                FullPath = file,
                RelativePath = relativePath.Replace('\\', '/'),
                Extension = extension
            };
        }

        _logger.LogInformation("Found {FileCount} files to process", fileCount);
    }

    private IEnumerable<string> EnumerateFiles(string directory, HashSet<string> excludedFolders)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Access denied to directory: {Directory}", directory);
            yield break;
        }
        catch (DirectoryNotFoundException)
        {
            yield break;
        }

        foreach (var file in files)
        {
            yield return file;
        }

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(directory);
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }
        catch (DirectoryNotFoundException)
        {
            yield break;
        }

        foreach (var subDir in directories)
        {
            var dirName = Path.GetFileName(subDir);

            // Skip excluded folders
            if (excludedFolders.Contains(dirName))
                continue;

            // Skip hidden folders
            if (dirName.StartsWith('.'))
                continue;

            foreach (var file in EnumerateFiles(subDir, excludedFolders))
            {
                yield return file;
            }
        }
    }

    private static bool IsExcluded(string fileName, List<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (MatchesPattern(fileName, pattern))
                return true;
        }
        return false;
    }

    private static bool MatchesPattern(string fileName, string pattern)
    {
        // Simple glob matching for *.ext patterns
        if (pattern.StartsWith("*."))
        {
            var ext = pattern[1..]; // Remove the *
            return fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase);
        }

        return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
