namespace LimelightX.UI.Services.Dto;

/// <summary>
/// POST /trace `data` payload (ui-data-contracts.md §3). Confirmed against
/// tests/api_trace.rs to have exactly these five keys - no separate
/// final_result field despite api.md §2.1's prose implying one. The final
/// result for trace mode is derived from the last ModelOutputs entry
/// (Phase 5's FinalResultViewModel), not from this DTO.
/// </summary>
public sealed class TraceData
{
    public required RawAstResponse RawAst { get; init; }

    public required NormalizedAstResponse NormalizedAst { get; init; }

    public required IrResponse Ir { get; init; }

    public IReadOnlyList<PromptBlock> Prompts { get; init; } = [];

    public IReadOnlyList<ModelOutputBlock> ModelOutputs { get; init; } = [];
}
