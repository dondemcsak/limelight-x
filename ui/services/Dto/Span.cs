namespace LimelightX.UI.Services.Dto;

/// <summary>
/// Byte-offset span shared by AST nodes, IR operations, and error locations
/// (ui-data-contracts.md §5.1, §5.4, §1). Currently always {0,0} on the wire
/// for AST/IR (the Rust parser doesn't track byte spans yet) - the UI must
/// tolerate that placeholder rather than depend on it.
/// </summary>
public sealed class Span
{
    public int Start { get; init; }

    public int End { get; init; }
}
