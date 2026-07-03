namespace LimelightX.UI.Services.Dto;

/// <summary>ui-data-contracts.md §4, §5.8. /run-only.</summary>
public sealed class FinalResult
{
    public required string Text { get; init; }

    public required ResultContentType ContentType { get; init; }
}
