namespace LimelightX.UI.Services.Dto;

/// <summary>`prompt_generated` event data (ui-data-contracts.md §7).</summary>
public sealed class PromptEventData
{
    public required PromptBlock Prompt { get; init; }
}
