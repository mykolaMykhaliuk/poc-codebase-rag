using CodebaseRag.Api.Configuration;

namespace CodebaseRag.Api.Services;

public interface IConfigurationManager
{
    RagSettings GetCurrentSettings();
    Task SaveSettingsAsync(RagSettings settings);
    Task ResetToDefaultsAsync();
    (bool IsValid, List<string> Errors) ValidateSettings(RagSettings settings);
}
