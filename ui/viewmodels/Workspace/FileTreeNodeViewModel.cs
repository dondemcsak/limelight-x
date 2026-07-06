using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LimelightX.UI.ViewModels.Workspace;

/// <summary>
/// A single file or folder in the Explorer tree (ui-viewmodels.md §4).
/// Directory nodes populate Children lazily the first time IsExpanded
/// flips true (FileTreeViewModel judgment call - "recursively scans" left
/// eager-vs-lazy unspecified; lazy keeps large folders responsive).
/// </summary>
public partial class FileTreeNodeViewModel : ObservableObject
{
    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public required bool IsDirectory { get; init; }

    [ObservableProperty]
    private bool _isExpanded;

    public ObservableCollection<FileTreeNodeViewModel> Children { get; } = [];

    /// <summary>Set by FileTreeViewModel once this directory's Children have been scanned, so re-collapsing/re-expanding doesn't rescan.</summary>
    internal bool ChildrenLoaded { get; set; }
}
