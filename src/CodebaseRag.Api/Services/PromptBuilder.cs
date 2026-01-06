using System.Text;
using CodebaseRag.Api.Configuration;
using Microsoft.Extensions.Options;

namespace CodebaseRag.Api.Services;

public class PromptBuilder : IPromptBuilder
{
    private readonly PromptSettings _settings;

    public PromptBuilder(IOptions<RagSettings> settings)
    {
        _settings = settings.Value.Prompt;
    }

    public string BuildPrompt(string question, IReadOnlyList<ScoredChunk> chunks)
    {
        var sb = new StringBuilder();

        // System section
        sb.AppendLine("[SYSTEM]");
        sb.AppendLine(_settings.SystemInstructions);
        sb.AppendLine();

        // Context section
        sb.AppendLine("[CONTEXT]");
        sb.AppendLine("The following code snippets are from the codebase and are relevant to your question.");
        sb.AppendLine("Use ONLY this information to answer. Do not make assumptions beyond what is shown.");
        sb.AppendLine();

        // Add code snippets
        var chunksToInclude = chunks.Take(_settings.MaxContextChunks).ToList();
        var totalTokens = 0;
        var estimatedTokensPerChar = 0.25; // Rough estimate

        foreach (var scoredChunk in chunksToInclude)
        {
            var chunk = scoredChunk.Chunk;
            var chunkTokens = (int)(chunk.Content.Length * estimatedTokensPerChar);

            // Check if we'd exceed max tokens
            if (totalTokens + chunkTokens > _settings.MaxContextTokens)
                break;

            totalTokens += chunkTokens;

            sb.AppendLine("---");
            sb.AppendLine($"File: {chunk.FilePath} (lines {chunk.StartLine}-{chunk.EndLine})");
            sb.AppendLine($"Language: {chunk.Language}");

            if (!string.IsNullOrEmpty(chunk.SymbolName))
            {
                sb.AppendLine($"Symbol: {chunk.SymbolName} ({chunk.SymbolType})");
            }

            sb.AppendLine();
            sb.AppendLine($"```{GetLanguageTag(chunk.Language)}");
            sb.AppendLine(chunk.Content);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        // Question section
        sb.AppendLine("[QUESTION]");
        sb.AppendLine(question);
        sb.AppendLine();

        // Instructions section
        sb.AppendLine("[INSTRUCTIONS]");
        sb.AppendLine("1. Answer based strictly on the provided code snippets");
        sb.AppendLine("2. Reference specific file paths and line numbers in your answer");
        sb.AppendLine("3. If the code doesn't contain enough information, state that clearly");
        sb.AppendLine("4. Do not invent or assume code that isn't shown");

        return sb.ToString();
    }

    private static string GetLanguageTag(string language)
    {
        return language switch
        {
            "csharp" => "csharp",
            "javascript" => "javascript",
            "plaintext" => "",
            _ => language
        };
    }
}
