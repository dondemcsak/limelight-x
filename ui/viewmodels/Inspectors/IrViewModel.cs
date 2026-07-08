using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.ViewModels.Inspectors;

/// <summary>ui-viewmodels.md §6.3.</summary>
public partial class IrViewModel : ObservableObject, IResizablePanelViewModel
{
    public ObservableCollection<IrOperation> Operations { get; } = [];

    [ObservableProperty]
    private string _rawText = string.Empty;

    [ObservableProperty]
    private IrMetadata? _metadata;

    [ObservableProperty]
    private bool _isCollapsed = true;

    /// <summary>This panel's current expanded height (ui-viewmodels.md §11), adjusted via its splitter handle in CnlTabView.</summary>
    [ObservableProperty]
    private double _height = InspectorPanelDefaults.DefaultHeight;

    public ObservableCollection<UiError> Errors { get; } = [];

    public IrViewModel()
    {
        Errors.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasErrors));
    }

    /// <summary>Set when this inspector's own data fails to render (ui-error-handling.md §6.3).</summary>
    public bool HasErrors => Errors.Count > 0;

    public void Reset()
    {
        Operations.Clear();
        RawText = string.Empty;
        Metadata = null;
        Errors.Clear();
        IsCollapsed = true;
    }
}
