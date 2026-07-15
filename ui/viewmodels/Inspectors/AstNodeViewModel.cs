using CommunityToolkit.Mvvm.ComponentModel;
using LimelightX.UI.Services.Dto;

namespace LimelightX.UI.ViewModels.Inspectors;

/// <summary>
/// Client-side expand state for one AstNode (ui-viewmodels.md §11.1-11.2,
/// ui-components.md §4.6). AstNode itself is an immutable wire DTO, so this
/// thin wrapper mirrors the FileTreeNodeViewModel/IsExpanded precedent
/// instead of mutating the DTO. Not part of the wire contract.
/// </summary>
public partial class AstNodeViewModel : ObservableObject
{
    public AstNode Node { get; }

    public string Type => Node.Type;

    public string Value => Node.Value;

    public IReadOnlyList<AstNodeViewModel> Children { get; }

    [ObservableProperty]
    private bool _isExpanded;

    private AstNodeViewModel(AstNode node, bool isExpanded)
    {
        Node = node;
        IsExpanded = isExpanded;
        Children = node.Children.Select(c => new AstNodeViewModel(c, isExpanded: false)).ToList();
    }

    /// <summary>Root defaults expanded (Depth == 0); every descendant defaults collapsed.</summary>
    public static AstNodeViewModel? FromDto(AstNode? root) =>
        root is null ? null : new AstNodeViewModel(root, isExpanded: root.Depth == 0);
}
