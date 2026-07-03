using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.ViewModels.Inspectors;

/// <summary>ui-viewmodels.md §6.4. Trace-only.</summary>
public partial class PromptViewModel : ObservableObject
{
    public ObservableCollection<PromptBlock> Prompts { get; } = [];

    [ObservableProperty]
    private string _rawText = string.Empty;

    [ObservableProperty]
    private bool _isCollapsed;

    public ObservableCollection<UiError> Errors { get; } = [];

    public void Reset()
    {
        Prompts.Clear();
        RawText = string.Empty;
        Errors.Clear();
    }
}
