namespace LimelightX.UI.Services.Dto;

/// <summary>`prompts_generated` event data (ui-data-contracts.md §7).</summary>
public sealed class PromptsEventData
{
    public IReadOnlyList<PromptBlock> Prompts { get; init; } = [];
}
