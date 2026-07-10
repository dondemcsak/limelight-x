using System.Runtime.InteropServices;

namespace LimelightX.UI.Intellisense;

/// <summary>
/// Tree-sitter's TSNode, mirrored field-for-field from tree_sitter/api.h so
/// P/Invoke marshaling is correct once NativeMethods.cs lands (see the
/// implementation plan's "Deferred to a later plan" section - this struct
/// carries no native calls of its own, it's a pure data shape). Declared now
/// so IParserHost/ICompletionService/etc. have a real return/parameter type
/// to compile against.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct TSNode
{
    public readonly uint Context0;
    public readonly uint Context1;
    public readonly uint Context2;
    public readonly uint Context3;
    public readonly IntPtr Id;
    public readonly IntPtr Tree;
}

/// <summary>Tree-sitter's TSPoint (tree_sitter/api.h) - a 0-based (row, column) source position.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct TSPoint
{
    public readonly uint Row;
    public readonly uint Column;
}
