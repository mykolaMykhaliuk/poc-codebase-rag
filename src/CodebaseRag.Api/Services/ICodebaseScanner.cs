namespace CodebaseRag.Api.Services;

public interface ICodebaseScanner
{
    IEnumerable<ScannedFile> ScanFiles();
}

public class ScannedFile
{
    public required string FullPath { get; set; }
    public required string RelativePath { get; set; }
    public required string Extension { get; set; }
}
