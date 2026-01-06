using CodebaseRag.Api.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodebaseRag.Api.Parsing;

public class CSharpParser : ICodeParser
{
    public string ParserType => "csharp";

    public IEnumerable<CodeChunk> Parse(string filePath, string content, ChunkingSettings settings)
    {
        if (string.IsNullOrWhiteSpace(content))
            yield break;

        SyntaxTree tree;
        try
        {
            tree = CSharpSyntaxTree.ParseText(content);
        }
        catch
        {
            // Fall back to plain text parsing if Roslyn fails
            var fallback = new PlainTextParser();
            foreach (var chunk in fallback.Parse(filePath, content, settings))
            {
                chunk.Language = "csharp";
                yield return chunk;
            }
            yield break;
        }

        var root = tree.GetCompilationUnitRoot();
        var chunks = new List<CodeChunk>();

        // Extract all members
        ExtractMembers(root, filePath, settings, chunks, null);

        // If no members found, fall back to plain text
        if (chunks.Count == 0)
        {
            var fallback = new PlainTextParser();
            foreach (var chunk in fallback.Parse(filePath, content, settings))
            {
                chunk.Language = "csharp";
                yield return chunk;
            }
            yield break;
        }

        foreach (var chunk in chunks)
        {
            yield return chunk;
        }
    }

    private void ExtractMembers(SyntaxNode node, string filePath, ChunkingSettings settings,
        List<CodeChunk> chunks, string? parentSymbol)
    {
        foreach (var child in node.ChildNodes())
        {
            switch (child)
            {
                case NamespaceDeclarationSyntax ns:
                    ExtractMembers(ns, filePath, settings, chunks, ns.Name.ToString());
                    break;

                case FileScopedNamespaceDeclarationSyntax fsns:
                    ExtractMembers(fsns, filePath, settings, chunks, fsns.Name.ToString());
                    break;

                case ClassDeclarationSyntax cls:
                    ExtractClassMembers(cls, filePath, settings, chunks, parentSymbol);
                    break;

                case StructDeclarationSyntax str:
                    ExtractStructMembers(str, filePath, settings, chunks, parentSymbol);
                    break;

                case InterfaceDeclarationSyntax iface:
                    ExtractInterfaceMembers(iface, filePath, settings, chunks, parentSymbol);
                    break;

                case RecordDeclarationSyntax rec:
                    ExtractRecordMembers(rec, filePath, settings, chunks, parentSymbol);
                    break;

                case EnumDeclarationSyntax enm:
                    AddChunk(enm, filePath, "enum", enm.Identifier.Text, parentSymbol, chunks);
                    break;

                case DelegateDeclarationSyntax del:
                    AddChunk(del, filePath, "delegate", del.Identifier.Text, parentSymbol, chunks);
                    break;

                default:
                    // Continue traversing
                    ExtractMembers(child, filePath, settings, chunks, parentSymbol);
                    break;
            }
        }
    }

    private void ExtractClassMembers(ClassDeclarationSyntax cls, string filePath,
        ChunkingSettings settings, List<CodeChunk> chunks, string? parentSymbol)
    {
        var className = BuildFullName(parentSymbol, cls.Identifier.Text);

        // Add class header (with attributes, modifiers, base types)
        var classHeader = GetClassHeader(cls);
        if (!string.IsNullOrEmpty(classHeader))
        {
            var headerSpan = cls.GetLocation().GetLineSpan();
            chunks.Add(new CodeChunk
            {
                FilePath = filePath,
                Language = "csharp",
                SymbolType = "class",
                SymbolName = className,
                ParentSymbol = parentSymbol,
                Content = classHeader,
                StartLine = headerSpan.StartLinePosition.Line + 1,
                EndLine = headerSpan.StartLinePosition.Line + 1
            });
        }

        // Extract methods, properties, etc.
        foreach (var member in cls.Members)
        {
            switch (member)
            {
                case MethodDeclarationSyntax method:
                    AddChunk(method, filePath, "method",
                        $"{className}.{method.Identifier.Text}", className, chunks);
                    break;

                case PropertyDeclarationSyntax prop:
                    AddChunk(prop, filePath, "property",
                        $"{className}.{prop.Identifier.Text}", className, chunks);
                    break;

                case ConstructorDeclarationSyntax ctor:
                    AddChunk(ctor, filePath, "constructor",
                        $"{className}.ctor", className, chunks);
                    break;

                case FieldDeclarationSyntax field:
                    var fieldNames = string.Join(", ", field.Declaration.Variables.Select(v => v.Identifier.Text));
                    AddChunk(field, filePath, "field",
                        $"{className}.{fieldNames}", className, chunks);
                    break;

                case ClassDeclarationSyntax nestedClass:
                    ExtractClassMembers(nestedClass, filePath, settings, chunks, className);
                    break;

                case StructDeclarationSyntax nestedStruct:
                    ExtractStructMembers(nestedStruct, filePath, settings, chunks, className);
                    break;

                case InterfaceDeclarationSyntax nestedInterface:
                    ExtractInterfaceMembers(nestedInterface, filePath, settings, chunks, className);
                    break;
            }
        }
    }

    private void ExtractStructMembers(StructDeclarationSyntax str, string filePath,
        ChunkingSettings settings, List<CodeChunk> chunks, string? parentSymbol)
    {
        var structName = BuildFullName(parentSymbol, str.Identifier.Text);

        foreach (var member in str.Members)
        {
            switch (member)
            {
                case MethodDeclarationSyntax method:
                    AddChunk(method, filePath, "method",
                        $"{structName}.{method.Identifier.Text}", structName, chunks);
                    break;

                case PropertyDeclarationSyntax prop:
                    AddChunk(prop, filePath, "property",
                        $"{structName}.{prop.Identifier.Text}", structName, chunks);
                    break;

                case ConstructorDeclarationSyntax ctor:
                    AddChunk(ctor, filePath, "constructor",
                        $"{structName}.ctor", structName, chunks);
                    break;

                case FieldDeclarationSyntax field:
                    var fieldNames = string.Join(", ", field.Declaration.Variables.Select(v => v.Identifier.Text));
                    AddChunk(field, filePath, "field",
                        $"{structName}.{fieldNames}", structName, chunks);
                    break;
            }
        }
    }

    private void ExtractInterfaceMembers(InterfaceDeclarationSyntax iface, string filePath,
        ChunkingSettings settings, List<CodeChunk> chunks, string? parentSymbol)
    {
        var ifaceName = BuildFullName(parentSymbol, iface.Identifier.Text);

        // Add the whole interface as one chunk (typically small)
        AddChunk(iface, filePath, "interface", ifaceName, parentSymbol, chunks);
    }

    private void ExtractRecordMembers(RecordDeclarationSyntax rec, string filePath,
        ChunkingSettings settings, List<CodeChunk> chunks, string? parentSymbol)
    {
        var recName = BuildFullName(parentSymbol, rec.Identifier.Text);

        // Add the whole record as one chunk (typically small)
        AddChunk(rec, filePath, "record", recName, parentSymbol, chunks);
    }

    private string GetClassHeader(ClassDeclarationSyntax cls)
    {
        var parts = new List<string>();

        // Add attributes
        foreach (var attrList in cls.AttributeLists)
        {
            parts.Add(attrList.ToString());
        }

        // Add modifiers and declaration
        var modifiers = cls.Modifiers.ToString();
        var keyword = cls.Keyword.Text;
        var name = cls.Identifier.Text;
        var typeParams = cls.TypeParameterList?.ToString() ?? "";
        var baseList = cls.BaseList?.ToString() ?? "";
        var constraints = string.Join(" ", cls.ConstraintClauses.Select(c => c.ToString()));

        var declaration = $"{modifiers} {keyword} {name}{typeParams}";
        if (!string.IsNullOrEmpty(baseList))
            declaration += $" {baseList}";
        if (!string.IsNullOrEmpty(constraints))
            declaration += $" {constraints}";

        parts.Add(declaration.Trim());
        return string.Join("\n", parts);
    }

    private void AddChunk(SyntaxNode node, string filePath, string symbolType,
        string symbolName, string? parentSymbol, List<CodeChunk> chunks)
    {
        var span = node.GetLocation().GetLineSpan();
        chunks.Add(new CodeChunk
        {
            FilePath = filePath,
            Language = "csharp",
            SymbolType = symbolType,
            SymbolName = symbolName,
            ParentSymbol = parentSymbol,
            Content = node.ToFullString().Trim(),
            StartLine = span.StartLinePosition.Line + 1,
            EndLine = span.EndLinePosition.Line + 1
        });
    }

    private static string BuildFullName(string? parent, string name)
    {
        return string.IsNullOrEmpty(parent) ? name : $"{parent}.{name}";
    }
}
