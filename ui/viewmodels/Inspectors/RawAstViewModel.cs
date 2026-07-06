using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.ViewModels.Inspectors;

/// <summary>ui-viewmodels.md §6.1.</summary>
public partial class RawAstViewModel : ObservableObject
{
    [ObservableProperty]
    private AstNode? _tree;

    [ObservableProperty]
    private string _rawText = string.Empty;

    [ObservableProperty]
    private AstMetadata? _metadata;

    [ObservableProperty]
    private bool _isCollapsed;

    public ObservableCollection<UiError> Errors { get; } = [];

    public RawAstViewModel()
    {
        Errors.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasErrors));
    }

    /// <summary>
    /// Set when this inspector's own data fails to render (ui-error-handling.md
    /// §6.3, ui-components.md §7.2) - independent of
    /// PipelineExecutionViewModel.HasErrors, which reflects a server-sent
    /// pipeline_failed event.
    /// </summary>
    public bool HasErrors => Errors.Count > 0;

    public void Reset()
    {
        Tree = null;
        RawText = string.Empty;
        Metadata = null;
        Errors.Clear();
    }
}
