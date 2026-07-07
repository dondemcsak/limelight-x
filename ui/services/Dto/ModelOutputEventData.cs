namespace LimelightX.UI.Services.Dto;

/// <summary>`model_output_generated` event data (ui-data-contracts.md §8).</summary>
public sealed class ModelOutputEventData
{
    public required ModelOutputBlock ModelOutput { get; init; }
}
