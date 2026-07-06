namespace LimelightX.UI.Services.Dto;

/// <summary>`model_outputs_generated` event data (ui-data-contracts.md §8).</summary>
public sealed class ModelOutputsEventData
{
    public IReadOnlyList<ModelOutputBlock> ModelOutputs { get; init; } = [];
}
