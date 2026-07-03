namespace LimelightX.UI.Services.Dto;

/// <summary>ui-data-contracts.md §3, §5.</summary>
public sealed class IrResponse
{
    public IReadOnlyList<IrOperation> Operations { get; init; } = [];

    public required string RawText { get; init; }

    public required IrMetadata Metadata { get; init; }
}
