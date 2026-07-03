namespace LimelightX.UI.Services.Dto;

/// <summary>
/// Full semantic AST node (ui-data-contracts.md §5.1). Root is a synthetic
/// "Program" node; Value is the Rust Debug-format string, not structured
/// fields - render it as-is. Span is currently always {0,0} (parser doesn't
/// track byte spans yet).
/// </summary>
public sealed class AstNode
{
    public required string Type { get; init; }

    public required string Value { get; init; }

    public IReadOnlyList<AstNode> Children { get; init; } = [];

    public required Span Span { get; init; }

    public int Depth { get; init; }

    public required AstNodeMetadata Metadata { get; init; }
}
