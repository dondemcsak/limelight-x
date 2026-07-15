namespace LimelightX.UI.Services;

/// <summary>Reads/writes %APPDATA%\LimelightX\config.json (ui-deployment.md §4.3). Never touches ApiKey - see ICredentialService.</summary>
public interface IConfigService
{
    string ConfigFilePath { get; }

    /// <summary>Returns null if the file is missing, unreadable, or not valid JSON matching the expected shape.</summary>
    AppConfig? Load();

    void Save(AppConfig config);
}
