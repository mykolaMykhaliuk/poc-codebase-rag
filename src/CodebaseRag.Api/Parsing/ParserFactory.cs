using CodebaseRag.Api.Configuration;

namespace CodebaseRag.Api.Parsing;

public interface IParserFactory
{
    ICodeParser GetParser(string fileExtension);
}

public class ParserFactory : IParserFactory
{
    private readonly Dictionary<string, ICodeParser> _parsers;
    private readonly Dictionary<string, string> _parserMapping;
    private readonly ICodeParser _fallbackParser;

    public ParserFactory(IEnumerable<ICodeParser> parsers, RagSettings settings)
    {
        _parsers = parsers.ToDictionary(p => p.ParserType, p => p);
        _parserMapping = settings.ParserMapping;
        _fallbackParser = _parsers.GetValueOrDefault("plaintext")
            ?? throw new InvalidOperationException("PlainTextParser must be registered");
    }

    public ICodeParser GetParser(string fileExtension)
    {
        var ext = fileExtension.ToLowerInvariant();
        if (!ext.StartsWith('.'))
            ext = "." + ext;

        if (_parserMapping.TryGetValue(ext, out var parserType))
        {
            if (_parsers.TryGetValue(parserType, out var parser))
            {
                return parser;
            }
        }

        return _fallbackParser;
    }
}
