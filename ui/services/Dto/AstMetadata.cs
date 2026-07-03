namespace LimelightX.UI.Services.Dto;

/// <summary>Raw AST response metadata (ui-data-contracts.md §5.2).</summary>
public sealed class AstMetadata
{
    public int NodeCount { get; init; }

    public int MaxDepth { get; init; }

    public int SourceLength { get; init; }
}
