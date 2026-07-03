using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.ViewModels.Inspectors;

/// <summary>ui-viewmodels.md §6.3.</summary>
public partial class IrViewModel : ObservableObject
{
    public ObservableCollection<IrOperation> Operations { get; } = [];

    [ObservableProperty]
    private string _rawText = string.Empty;

    [ObservableProperty]
    private IrMetadata? _metadata;

    [ObservableProperty]
    private bool _isCollapsed;

    public ObservableCollection<UiError> Errors { get; } = [];

    public void Reset()
    {
        Operations.Clear();
        RawText = string.Empty;
        Metadata = null;
        Errors.Clear();
    }
}
