using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.ViewModels.Tabs;

/// <summary>
/// Base type for one open file's tab (ui-viewmodels.md §5.1). Concrete
/// subtypes are CnlTabViewModel (.llx files) and PlainTextTabViewModel
/// (everything else). A tab may be untitled (created via File > New LLX/TXT
/// File, ui-viewmodels.md §3) - it has no FilePath until Save/Save As
/// assigns one via AssignFilePath.
/// </summary>
public abstract partial class TabViewModel : ObservableObject, IDisposable
{
    protected TabViewModel(string? filePath, string header)
    {
        FilePath = filePath;
        IsUntitled = filePath is null;
        Header = header;
    }

    public string? FilePath { get; private set; }

    [ObservableProperty]
    private bool _isUntitled;

    [ObservableProperty]
    private string _header;

    [ObservableProperty]
    private bool _isDirty;

    /// <summary>Set by WorkspaceViewModel whenever ActiveTab changes (ui-components.md §3.2) - lets TabStrip highlight the active tab without an ambient binding back to WorkspaceViewModel.</summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>
    /// Surfaces Open/Save/Save As/Save All failures for this tab
    /// (ui-viewmodels.md §13) - distinct from a CnlTabViewModel's
    /// PipelineExecution.ErrorBanner, which stays scoped to execution only.
    /// </summary>
    public ErrorBannerViewModel ErrorBanner { get; } = new();

    /// <summary>Set by WorkspaceViewModel to CloseTabCommand(this) when the tab is created - keeps this base type free of a compile-time WorkspaceViewModel dependency.</summary>
    public Action? CloseRequested { get; set; }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    /// <summary>Called only by WorkspaceViewModel's save flow once an untitled tab is written to disk for the first time, or when Save As redirects an existing tab to a new path.</summary>
    internal void AssignFilePath(string filePath)
    {
        FilePath = filePath;
        Header = Path.GetFileName(filePath);
        IsUntitled = false;
    }

    /// <summary>Called by WorkspaceViewModel's save flow once the tab's content has been written to disk. CnlTabViewModel overrides this to also re-anchor its dirty-flag baseline to the just-saved text.</summary>
    internal virtual void MarkAsSaved() => IsDirty = false;

    public abstract void Dispose();
}
