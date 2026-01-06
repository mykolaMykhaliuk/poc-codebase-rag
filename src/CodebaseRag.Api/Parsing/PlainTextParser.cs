using CodebaseRag.Api.Configuration;

namespace CodebaseRag.Api.Parsing;

public class PlainTextParser : ICodeParser
{
    public string ParserType => "plaintext";

    public IEnumerable<CodeChunk> Parse(string filePath, string content, ChunkingSettings settings)
    {
        if (string.IsNullOrWhiteSpace(content))
            yield break;

        var lines = content.Split('\n');
        var currentChunk = new List<string>();
        var currentStartLine = 1;
        var currentLength = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineLength = line.Length + 1; // +1 for newline

            if (currentLength + lineLength > settings.MaxChunkSize && currentChunk.Count > 0)
            {
                // Yield current chunk
                yield return new CodeChunk
                {
                    FilePath = filePath,
                    Language = "plaintext",
                    SymbolType = "text",
                    Content = string.Join("\n", currentChunk),
                    StartLine = currentStartLine,
                    EndLine = currentStartLine + currentChunk.Count - 1
                };

                // Calculate overlap - keep last N characters worth of lines
                var overlapLines = new List<string>();
                var overlapLength = 0;
                for (var j = currentChunk.Count - 1; j >= 0 && overlapLength < settings.ChunkOverlap; j--)
                {
                    overlapLines.Insert(0, currentChunk[j]);
                    overlapLength += currentChunk[j].Length + 1;
                }

                currentChunk = overlapLines;
                currentStartLine = i + 1 - overlapLines.Count + 1;
                currentLength = overlapLength;
            }

            currentChunk.Add(line);
            currentLength += lineLength;
        }

        // Yield remaining content
        if (currentChunk.Count > 0)
        {
            yield return new CodeChunk
            {
                FilePath = filePath,
                Language = "plaintext",
                SymbolType = "text",
                Content = string.Join("\n", currentChunk),
                StartLine = currentStartLine,
                EndLine = currentStartLine + currentChunk.Count - 1
            };
        }
    }
}
