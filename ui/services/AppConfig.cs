namespace LimelightX.UI.Services;

/// <summary>
/// In-memory representation of config.json, colocated with LimelightX.exe
/// (ui-deployment.md §4.3). A record (not a plain class) so callers can
/// non-destructively update a subset of fields via `with` - notably
/// SettingsViewModel.SaveSettingsAsync, which must preserve
/// LastOpenedFolder/RecentFolders (fields it never edits) rather than
/// resetting them to their defaults on every Settings save.
/// </summary>
public sealed record AppConfig
{
    public int Port { get; init; } = 4747;

    public string LogPath { get; init; } = string.Empty;

    public EnvironmentProfile EnvironmentProfile { get; init; } = EnvironmentProfile.Dev;

    /// <summary>Restored on startup (ui-routing-navigation.md §4.1) if the directory still exists; null on first launch or if it was deleted/moved since.</summary>
    public string? LastOpenedFolder { get; init; }

    /// <summary>Bounded to the 5 most recently opened folders, most-recent-first, no duplicates.</summary>
    public IReadOnlyList<string> RecentFolders { get; init; } = [];
}
