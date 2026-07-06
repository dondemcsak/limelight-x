using CommunityToolkit.Mvvm.ComponentModel;

namespace LimelightX.UI.ViewModels.Tabs;

/// <summary>
/// Generic text editing state for a non-.llx tab (ui-viewmodels.md §5.4) -
/// no validation, no syntax highlighting, no pipeline commands.
/// </summary>
public partial class PlainTextEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private int _cursorPosition;

    [ObservableProperty]
    private bool _isDirty;
}
