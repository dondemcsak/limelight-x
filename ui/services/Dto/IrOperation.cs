namespace LimelightX.UI.Services.Dto;

/// <summary>Full IR operation structure (ui-data-contracts.md §5.4). Input/Prompt/Target are optional per operation type.</summary>
public sealed class IrOperation
{
    public int OperationIndex { get; init; }

    public required string Type { get; init; }

    public int? Input { get; init; }

    public string? Prompt { get; init; }

    public string? Target { get; init; }

    public required Span SourceSpan { get; init; }

    public required string NormalizedSource { get; init; }

    public required DebugInfo DebugInfo { get; init; }
}
