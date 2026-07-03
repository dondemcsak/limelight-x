namespace LimelightX.UI.Services.Dto;

/// <summary>
/// IR operation debug info (ui-data-contracts.md §5.4). EstimatedCost is
/// currently always 0.0 server-side (no cost model wired up) - render it but
/// do not treat it as meaningful yet.
/// </summary>
public sealed class DebugInfo
{
    public int TokenCount { get; init; }

    public double EstimatedCost { get; init; }
}
