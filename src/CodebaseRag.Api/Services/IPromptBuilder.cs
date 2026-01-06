namespace CodebaseRag.Api.Services;

public interface IPromptBuilder
{
    string BuildPrompt(string question, IReadOnlyList<ScoredChunk> chunks);
}
