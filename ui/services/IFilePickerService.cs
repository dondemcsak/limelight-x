namespace LimelightX.UI.Services;

/// <summary>
/// Abstraction over Avalonia's IStorageProvider so FileLoaderViewModel does
/// not need a compile-time reference to any concrete View (ui-architecture.md
/// §3: "ViewModels must not reference Views").
/// </summary>
public interface IFilePickerService
{
    /// <summary>Shows an OS file picker filtered to .llx files; returns the
    /// selected local path, or null if the user cancelled.</summary>
    Task<string?> PickCnlFileAsync();
}
