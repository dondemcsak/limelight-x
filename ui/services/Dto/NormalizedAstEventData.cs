namespace LimelightX.UI.Services.Dto;

/// <summary>`normalized_ast_generated` event data (ui-data-contracts.md §5).</summary>
public sealed class NormalizedAstEventData
{
    public required NormalizedAstResponse NormalizedAst { get; init; }
}
