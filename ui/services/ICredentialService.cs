namespace LimelightX.UI.Services;

/// <summary>
/// Wraps Windows Credential Manager for the single shared ANTHROPIC_API_KEY
/// credential (ui-viewmodels.md §3.3) - never written to config.json.
/// </summary>
public interface ICredentialService
{
    string? ReadApiKey();

    void WriteApiKey(string apiKey);

    void DeleteApiKey();
}
