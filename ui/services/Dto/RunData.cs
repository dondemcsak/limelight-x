namespace LimelightX.UI.Services.Dto;

/// <summary>POST /run `data` payload (ui-data-contracts.md §4).</summary>
public sealed class RunData
{
    public required FinalResult FinalResult { get; init; }
}
