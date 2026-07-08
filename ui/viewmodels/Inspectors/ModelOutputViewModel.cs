using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.ViewModels.Inspectors;

/// <summary>ui-viewmodels.md §6.5. Trace-only.</summary>
public partial class ModelOutputViewModel : ObservableObject, IResizablePanelViewModel
{
    public ObservableCollection<ModelOutputBlock> Outputs { get; } = [];

    [ObservableProperty]
    private string _rawText = string.Empty;

    [ObservableProperty]
    private bool _isCollapsed = true;

    /// <summary>This panel's current expanded height (ui-viewmodels.md §11), adjusted via its splitter handle in CnlTabView.</summary>
    [ObservableProperty]
    private double _height = InspectorPanelDefaults.DefaultHeight;

    public ObservableCollection<UiError> Errors { get; } = [];

    public ModelOutputViewModel()
    {
        Errors.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasErrors));
        Outputs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasOutputs));
    }

    /// <summary>Set when this inspector's own data fails to render (ui-error-handling.md §6.3).</summary>
    public bool HasErrors => Errors.Count > 0;

    /// <summary>Whether any outputs have arrived yet - true from the first `model_output_generated` event, false again after <see cref="Reset"/>. No longer gates the panel's own visibility (ui-components.md §5.1: the panel is always rendered, starting collapsed); retained as a convenience for any other consumer.</summary>
    public bool HasOutputs => Outputs.Count > 0;

    public void Reset()
    {
        Outputs.Clear();
        RawText = string.Empty;
        Errors.Clear();
        IsCollapsed = true;
    }
}
