using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.ViewModels.Inspectors;

/// <summary>ui-viewmodels.md §6.5. Trace-only.</summary>
public partial class ModelOutputViewModel : ObservableObject
{
    public ObservableCollection<ModelOutputBlock> Outputs { get; } = [];

    [ObservableProperty]
    private string _rawText = string.Empty;

    [ObservableProperty]
    private bool _isCollapsed;

    public ObservableCollection<UiError> Errors { get; } = [];

    public void Reset()
    {
        Outputs.Clear();
        RawText = string.Empty;
        Errors.Clear();
    }
}
