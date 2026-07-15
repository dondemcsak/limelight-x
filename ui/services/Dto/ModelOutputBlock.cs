namespace LimelightX.UI.Services.Dto;

/// <summary>
/// Full model output structure (ui-data-contracts.md §5.7). Trace-only.
/// </summary>
public sealed class ModelOutputBlock
{
    public int OperationIndex { get; init; }

    public required string RawText { get; init; }

    public required ResultContentType ContentType { get; init; }

    public required ParsedContent Parsed { get; init; }

    public required ModelOutputMetadata Metadata { get; init; }
}
