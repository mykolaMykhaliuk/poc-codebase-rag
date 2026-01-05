using System.Text.RegularExpressions;
using CodebaseRag.Api.Configuration;

namespace CodebaseRag.Api.Parsing;

public class JavaScriptParser : ICodeParser
{
    public string ParserType => "javascript";

    // Regex patterns for JavaScript constructs
    private static readonly Regex FunctionDeclarationRegex = new(
        @"^(?<export>export\s+)?(?<async>async\s+)?function\s+(?<name>\w+)\s*\([^)]*\)\s*\{",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex ArrowFunctionRegex = new(
        @"^(?<export>export\s+)?(?<kind>const|let|var)\s+(?<name>\w+)\s*=\s*(?<async>async\s+)?\([^)]*\)\s*=>",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex ClassDeclarationRegex = new(
        @"^(?<export>export\s+)?(?<default>default\s+)?class\s+(?<name>\w+)(?:\s+extends\s+\w+)?\s*\{",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex ExportDefaultRegex = new(
        @"^export\s+default\s+",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex ModuleExportsRegex = new(
        @"^module\.exports\s*=",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public IEnumerable<CodeChunk> Parse(string filePath, string content, ChunkingSettings settings)
    {
        if (string.IsNullOrWhiteSpace(content))
            yield break;

        var lines = content.Split('\n');
        var chunks = new List<CodeChunk>();
        var usedRanges = new List<(int start, int end)>();

        // Find all function declarations
        foreach (Match match in FunctionDeclarationRegex.Matches(content))
        {
            var chunk = ExtractBlock(content, lines, match, "function", match.Groups["name"].Value, filePath);
            if (chunk != null && !IsOverlapping(usedRanges, chunk.StartLine, chunk.EndLine))
            {
                chunks.Add(chunk);
                usedRanges.Add((chunk.StartLine, chunk.EndLine));
            }
        }

        // Find all arrow functions
        foreach (Match match in ArrowFunctionRegex.Matches(content))
        {
            var chunk = ExtractBlock(content, lines, match, "function", match.Groups["name"].Value, filePath);
            if (chunk != null && !IsOverlapping(usedRanges, chunk.StartLine, chunk.EndLine))
            {
                chunks.Add(chunk);
                usedRanges.Add((chunk.StartLine, chunk.EndLine));
            }
        }

        // Find all class declarations
        foreach (Match match in ClassDeclarationRegex.Matches(content))
        {
            var chunk = ExtractBlock(content, lines, match, "class", match.Groups["name"].Value, filePath);
            if (chunk != null && !IsOverlapping(usedRanges, chunk.StartLine, chunk.EndLine))
            {
                chunks.Add(chunk);
                usedRanges.Add((chunk.StartLine, chunk.EndLine));
            }
        }

        // If we found structured elements, return them
        if (chunks.Count > 0)
        {
            // Sort by start line
            foreach (var chunk in chunks.OrderBy(c => c.StartLine))
            {
                yield return chunk;
            }
            yield break;
        }

        // Fallback to plain text chunking if no structured elements found
        var fallback = new PlainTextParser();
        foreach (var chunk in fallback.Parse(filePath, content, settings))
        {
            chunk.Language = "javascript";
            yield return chunk;
        }
    }

    private CodeChunk? ExtractBlock(string content, string[] lines, Match match,
        string symbolType, string symbolName, string filePath)
    {
        var startIndex = match.Index;
        var startLine = content[..startIndex].Count(c => c == '\n') + 1;

        // Find the matching closing brace
        var braceCount = 0;
        var inString = false;
        var stringChar = '\0';
        var inComment = false;
        var inMultiLineComment = false;
        var foundStart = false;
        var endIndex = startIndex;

        for (var i = match.Index; i < content.Length; i++)
        {
            var c = content[i];
            var prev = i > 0 ? content[i - 1] : '\0';
            var next = i < content.Length - 1 ? content[i + 1] : '\0';

            // Handle strings
            if (!inComment && !inMultiLineComment)
            {
                if ((c == '"' || c == '\'' || c == '`') && prev != '\\')
                {
                    if (inString && c == stringChar)
                        inString = false;
                    else if (!inString)
                    {
                        inString = true;
                        stringChar = c;
                    }
                }
            }

            // Handle comments
            if (!inString)
            {
                if (c == '/' && next == '/' && !inMultiLineComment)
                    inComment = true;
                if (c == '\n')
                    inComment = false;
                if (c == '/' && next == '*' && !inComment)
                    inMultiLineComment = true;
                if (c == '*' && next == '/' && inMultiLineComment)
                {
                    inMultiLineComment = false;
                    continue;
                }
            }

            // Count braces
            if (!inString && !inComment && !inMultiLineComment)
            {
                if (c == '{')
                {
                    braceCount++;
                    foundStart = true;
                }
                else if (c == '}')
                {
                    braceCount--;
                    if (foundStart && braceCount == 0)
                    {
                        endIndex = i;
                        break;
                    }
                }
            }
        }

        // Handle arrow functions without braces (single expression)
        if (!foundStart && symbolType == "function")
        {
            // Find the end of the line or semicolon
            var lineEnd = content.IndexOf('\n', startIndex);
            var semiColon = content.IndexOf(';', startIndex);

            if (semiColon != -1 && (lineEnd == -1 || semiColon < lineEnd))
                endIndex = semiColon;
            else if (lineEnd != -1)
                endIndex = lineEnd;
            else
                endIndex = content.Length - 1;
        }

        if (endIndex <= startIndex)
            return null;

        var endLine = content[..endIndex].Count(c => c == '\n') + 1;
        var blockContent = content.Substring(startIndex, endIndex - startIndex + 1).Trim();

        return new CodeChunk
        {
            FilePath = filePath,
            Language = "javascript",
            SymbolType = symbolType,
            SymbolName = symbolName,
            Content = blockContent,
            StartLine = startLine,
            EndLine = endLine
        };
    }

    private static bool IsOverlapping(List<(int start, int end)> ranges, int start, int end)
    {
        return ranges.Any(r => !(end < r.start || start > r.end));
    }
}
