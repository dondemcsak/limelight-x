namespace LimelightX.UI.Services.Dto;

/// <summary>
/// Prompt text + index + metadata (ui-data-contracts.md §5.6). Trace-only:
/// only present for model-calling operations - a Load operation, for
/// example, produces no prompt block (confirmed against tests/api_trace.rs).
/// </summary>
public sealed class PromptBlock
{
    public int OperationIndex { get; init; }

    public required string PromptText { get; init; }

    public required PromptBlockMetadata Metadata { get; init; }
}
