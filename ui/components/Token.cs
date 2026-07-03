namespace LimelightX.UI.Components;

/// <summary>A classified span of CNL source text, [Start, End) in UTF-16 code units.</summary>
public readonly record struct Token(int Start, int End, TokenKind Kind)
{
    public int Length => End - Start;
}
