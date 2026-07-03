namespace LimelightX.UI.Services.Dto;

/// <summary>ui-data-contracts.md §5.5.</summary>
public sealed class IrMetadata
{
    public int OperationCount { get; init; }

    public int MaxDepth { get; init; }

    public IReadOnlyDictionary<string, int> ReferenceMap { get; init; } = new Dictionary<string, int>();
}
