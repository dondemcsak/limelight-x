namespace LimelightX.UI.Services.Dto;

/// <summary>Normalized AST response metadata (ui-data-contracts.md §5.3).</summary>
public sealed class NormalizedAstMetadata
{
    public int NodeCount { get; init; }

    public int MaxDepth { get; init; }

    public int NormalizationSteps { get; init; }

    public int RemovedNamedVariables { get; init; }

    public int AddedInputRefs { get; init; }
}
