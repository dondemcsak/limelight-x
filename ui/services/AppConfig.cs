using LimelightX.UI.Routing;

namespace LimelightX.UI.Services;

/// <summary>In-memory representation of %APPDATA%\LimelightX\config.json (ui-deployment.md §4.3).</summary>
public sealed class AppConfig
{
    public int Port { get; init; } = 4747;

    public string LogPath { get; init; } = string.Empty;

    public EnvironmentProfile EnvironmentProfile { get; init; } = EnvironmentProfile.Dev;
}
