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

    public void Reset()
    {
        Tree = null;
        RawText = string.Empty;
        Metadata = null;
        Errors.Clear();
    }
}
