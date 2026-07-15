namespace LimelightX.UI.Services.Dto;

/// <summary>ui-data-contracts.md §5.7.</summary>
public sealed class ModelOutputMetadata
{
    public int TokenUsage { get; init; }

    public int LatencyMs { get; init; }
}
