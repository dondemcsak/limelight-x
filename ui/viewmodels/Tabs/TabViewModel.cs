using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LimelightX.UI.ViewModels.Tabs;

/// <summary>
/// Base type for one open file's tab (ui-viewmodels.md §5.1). Concrete
/// subtypes are CnlTabViewModel (.llx files) and PlainTextTabViewModel
/// (everything else).
/// </summary>
public abstract partial class TabViewModel : ObservableObject, IDisposable
{
    protected TabViewModel(string filePath)
    {
        FilePath = filePath;
        Header = Path.GetFileName(filePath);
    }

    public string FilePath { get; }

    [ObservableProperty]
    private string _header;

    [ObservableProperty]
    private bool _isDirty;

    /// <summary>Set by WorkspaceViewModel whenever ActiveTab changes (ui-components.md §3.2) - lets TabStrip highlight the active tab without an ambient binding back to WorkspaceViewModel.</summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>Set by WorkspaceViewModel to CloseTabCommand(this) when the tab is created - keeps this base type free of a compile-time WorkspaceViewModel dependency.</summary>
    public Action? CloseRequested { get; set; }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    public abstract void Dispose();
}
