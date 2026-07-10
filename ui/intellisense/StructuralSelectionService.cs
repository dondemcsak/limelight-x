namespace LimelightX.UI.Intellisense;

/// <summary>
/// Real Tree-sitter-backed implementation. Repeatedly re-resolving
/// [startByte, endByte) via ts_node_descendant_for_byte_range and walking to
/// ts_node_parent isn't enough on its own: several grammar rules wrap a
/// child with an identical byte span (e.g. "it" -> pronoun -> input all
/// spanning the same two bytes in "Summarize it."), so a naive
/// walk-one-parent-and-stop would re-resolve straight back down to the same
/// leaf every call and never actually expand. Instead this walks up past
/// every ancestor whose span doesn't yet exceed the current selection,
/// stopping at the first one that's strictly larger - equivalent to "one
/// grammar-meaningful expansion step," collapsing any same-span wrapper
/// layers into that single step rather than getting stuck on them.
/// </summary>
public sealed class StructuralSelectionService : IStructuralSelectionService
{
    public (int Start, int End) ExpandSelection(TSNode root, int startByte, int endByte)
    {
        var node = NativeMethods.ts_node_descendant_for_byte_range(root, (uint)startByte, (uint)endByte);

        while (!NativeMethods.ts_node_is_null(node)
            && (int)NativeMethods.ts_node_start_byte(node) >= startByte
            && (int)NativeMethods.ts_node_end_byte(node) <= endByte)
        {
            node = NativeMethods.ts_node_parent(node);
        }

        return NativeMethods.ts_node_is_null(node)
            ? (startByte, endByte)
            : ((int)NativeMethods.ts_node_start_byte(node), (int)NativeMethods.ts_node_end_byte(node));
    }
}
