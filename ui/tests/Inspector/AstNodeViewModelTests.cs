using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Inspectors;
using Xunit;

namespace LimelightX.UI.Tests.Inspector;

/// <summary>bdd-ui-interactions.md §4.14-§4.15: AST tree root auto-expand.</summary>
public class AstNodeViewModelTests
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

    [Fact]
    public void FromDto_RootDefaultsExpanded_DescendantsCollapsed()
    {
        var child = MakeNode(depth: 1);
        var root = MakeNode(depth: 0, children: [child]);

        var viewModel = AstNodeViewModel.FromDto(root);

        Assert.NotNull(viewModel);
        Assert.True(viewModel!.IsExpanded);
        Assert.Single(viewModel.Children);
        Assert.False(viewModel.Children[0].IsExpanded);
    }

    [Fact]
    public void FromDto_NullRoot_ReturnsNull()
    {
        Assert.Null(AstNodeViewModel.FromDto(null));
    }

    [Fact]
    public void ToggleIsExpanded_OnChild_DoesNotAffectSiblingsOrRoot()
    {
        var childA = MakeNode(depth: 1);
        var childB = MakeNode(depth: 1);
        var root = MakeNode(depth: 0, children: [childA, childB]);

        var viewModel = AstNodeViewModel.FromDto(root)!;
        viewModel.Children[0].IsExpanded = true;

        Assert.True(viewModel.Children[0].IsExpanded);
        Assert.False(viewModel.Children[1].IsExpanded);
        Assert.True(viewModel.IsExpanded);
    }
}
