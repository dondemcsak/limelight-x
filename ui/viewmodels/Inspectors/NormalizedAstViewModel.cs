using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.ViewModels.Inspectors;

/// <summary>ui-viewmodels.md §6.2.</summary>
public partial class NormalizedAstViewModel : ObservableObject
{
    [ObservableProperty]
    private AstNode? _tree;

    [ObservableProperty]
    private string _rawText = string.Empty;

    [ObservableProperty]
    private NormalizedAstMetadata? _metadata;

    [ObservableProperty]
    private bool _isCollapsed;

    public ObservableCollection<UiError> Errors { get; } = [];

    public NormalizedAstViewModel()
    {
        Errors.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasErrors));
    }

    /// <summary>Set when this inspector's own data fails to render (ui-error-handling.md §6.3).</summary>
    public bool HasErrors => Errors.Count > 0;

    public void Reset()
    {
        Tree = null;
        RawText = string.Empty;
        Metadata = null;
        Errors.Clear();
    }
}
