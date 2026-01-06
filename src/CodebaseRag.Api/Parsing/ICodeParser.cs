using CodebaseRag.Api.Configuration;

namespace CodebaseRag.Api.Parsing;

public interface ICodeParser
{
    string ParserType { get; }
    IEnumerable<CodeChunk> Parse(string filePath, string content, ChunkingSettings settings);
}
