namespace LimelightX.UI.ViewModels;

/// <summary>Shape for EditorViewModel.HoverInfo, returned (nullable) by IHoverService.GetHover - null, not an Empty sentinel, means no hover content at that position.</summary>
public sealed class HoverInfo
{
    public required string Text { get; init; }

    public int Position { get; init; }
}
