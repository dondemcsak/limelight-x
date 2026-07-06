using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.ViewModels.Workspace;

/// <summary>
/// Scans the currently open root folder (ui-viewmodels.md §4) - a pure
/// client-side filesystem read, no backend endpoint involved. Tracks
/// expand/collapse state per node and surfaces the selected file so
/// WorkspaceViewModel can open/focus its tab.
/// </summary>
public partial class FileTreeViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _rootPath;

    [ObservableProperty]
    private FileTreeNodeViewModel? _selectedNode;

    public ObservableCollection<FileTreeNodeViewModel> Nodes { get; } = [];

    /// <summary>Surfaced filesystem read errors (e.g. permission denied) - same UiError shape as any other error surface.</summary>
    public ObservableCollection<UiError> Errors { get; } = [];

    public void OpenRoot(string path)
    {
        RootPath = path;
        SelectedNode = null;
        Errors.Clear();
        Nodes.Clear();

        foreach (var node in ScanDirectory(path))
        {
            Nodes.Add(node);
        }
    }

    private IReadOnlyList<FileTreeNodeViewModel> ScanDirectory(string directoryPath)
    {
        try
        {
            var directories = Directory
                .GetDirectories(directoryPath)
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .Select(d => CreateNode(d, isDirectory: true));

            var files = Directory
                .GetFiles(directoryPath)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(f => CreateNode(f, isDirectory: false));

            return [.. directories, .. files];
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Errors.Add(new UiError
            {
                Code = "ERR_FILE_TREE_READ",
                Message = $"Could not read folder \"{directoryPath}\": {ex.Message}",
                Severity = ErrorSeverity.Warning,
                Category = ErrorCategory.State,
            });
            return [];
        }
    }

    private FileTreeNodeViewModel CreateNode(string path, bool isDirectory)
    {
        var node = new FileTreeNodeViewModel
        {
            Name = Path.GetFileName(path),
            FullPath = path,
            IsDirectory = isDirectory,
        };

        if (isDirectory)
        {
            node.PropertyChanged += OnNodePropertyChanged;
        }

        return node;
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FileTreeNodeViewModel.IsExpanded))
        {
            return;
        }

        var node = (FileTreeNodeViewModel)sender!;
        if (!node.IsExpanded || node.ChildrenLoaded)
        {
            return;
        }

        node.ChildrenLoaded = true;
        foreach (var child in ScanDirectory(node.FullPath))
        {
            node.Children.Add(child);
        }
    }
}
