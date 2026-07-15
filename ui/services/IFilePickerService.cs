namespace LimelightX.UI.Services;

/// <summary>
/// Abstraction over Avalonia's IStorageProvider so WorkspaceViewModel does
/// not need a compile-time reference to any concrete View (ui-architecture.md
/// §3: "ViewModels must not reference Views").
/// </summary>
public interface IFilePickerService
{
    /// <summary>Shows an OS file picker filtered to .llx files; returns the
    /// selected local path, or null if the user cancelled.</summary>
    Task<string?> PickCnlFileAsync();

    /// <summary>Shows an OS folder picker (WorkspaceViewModel.OpenFolderCommand); returns the selected local path, or null if the user cancelled.</summary>
    Task<string?> PickFolderAsync();

    /// <summary>Shows an unfiltered OS file picker (WorkspaceViewModel.OpenFileCommand, ui-viewmodels.md §3); returns the selected local path, or null if the user cancelled.</summary>
    Task<string?> PickAnyFileAsync();

    /// <summary>Shows an OS save-file picker (WorkspaceViewModel.SaveCommand/SaveAsCommand, ui-viewmodels.md §3), suggesting <paramref name="suggestedFileName"/> and <paramref name="defaultExtension"/> (no leading dot, e.g. "llx"); returns the chosen local path, or null if the user cancelled.</summary>
    Task<string?> PickSaveFileAsync(string suggestedFileName, string? defaultExtension);
}
