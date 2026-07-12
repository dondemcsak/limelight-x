using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LimelightX.UI.Components;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Inspectors;
using Xunit;

namespace LimelightX.UI.Tests.Inspector;

/// <summary>
/// Headless render tests for AstTreeView's root auto-expand
/// (ui-components.md §4.6, bdd-ui-interactions.md §4.14-§4.15) - the
/// TreeViewItem IsExpanded style binding is only exercised by actually
/// constructing and attaching the control (see WorkspaceShellRenderTests).
/// </summary>
public class AstTreeViewRenderTests
{
    private static AstNode MakeNode(int depth, IReadOnlyList<AstNode>? children = null) => new()
    {
        Type = depth == 0 ? "Program" : "Load",
        Value = string.Empty,
        Span = new Span(),
        Depth = depth,
        Metadata = new AstNodeMetadata(),
        Children = children ?? [],
    };

    [AvaloniaFact]
    public void AstTreeView_Root_TreeViewItemIsExpandedByDefault()
    {
        var root = AstNodeViewModel.FromDto(MakeNode(depth: 0, children: [MakeNode(depth: 1)]))!;
        var view = new AstTreeView { RootNode = root };
        var window = new Window { Content = view, Width = 400, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var treeView = view.FindControl<TreeView>("Tree")!;
        var rootContainer = (TreeViewItem)treeView.ContainerFromIndex(0)!;

        Assert.True(rootContainer.IsExpanded);
    }

    [AvaloniaFact]
    public void AstTreeView_TogglingChildIsExpanded_LeavesRootExpanded()
    {
        var child = MakeNode(depth: 1);
        var root = AstNodeViewModel.FromDto(MakeNode(depth: 0, children: [child]))!;
        var view = new AstTreeView { RootNode = root };
        var window = new Window { Content = view, Width = 400, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        root.Children[0].IsExpanded = true;
        Dispatcher.UIThread.RunJobs();

        var treeView = view.FindControl<TreeView>("Tree")!;
        var rootContainer = (TreeViewItem)treeView.ContainerFromIndex(0)!;

        Assert.True(root.IsExpanded);
        Assert.True(rootContainer.IsExpanded);
    }
}
