using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LimelightX.UI.ViewModels.Errors;

/// <summary>
/// Backs the ErrorBanner component (ui-components.md §7.1, ui-error-handling.md
/// §9). One instance per tab's PipelineExecutionViewModel, one on
/// SettingsViewModel, and one on WorkspaceViewModel (filesystem errors) - never
/// a single app-wide instance, so switching tabs never shows or hides another
/// tab's banner.
/// </summary>
public partial class ErrorBannerViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private ErrorSeverity _severity;

    public ObservableCollection<UiError> Errors { get; } = [];

    [RelayCommand]
    private void Dismiss()
    {
        IsVisible = false;
        Errors.Clear();
    }

    public void Show(UiError error)
    {
        Errors.Add(error);
        Severity = error.Severity;
        IsVisible = true;
    }

    public void Show(IEnumerable<UiError> errors)
    {
        foreach (var error in errors)
        {
            Show(error);
        }
    }

    public void Clear()
    {
        Errors.Clear();
        IsVisible = false;
    }
}
