using System.Runtime.InteropServices;
using LimelightX.UI.Intellisense;

namespace LimelightX.UI.Tests.Intellisense;

/// <summary>Small shared helpers for walking a real TSNode tree in tests - thin wrappers over NativeMethods (internal, visible here via InternalsVisibleTo in ui/intellisense/NativeMethods.cs).</summary>
internal static class TreeSitterTestHelpers
{
    public static string NodeType(TSNode node) =>
        Marshal.PtrToStringUTF8(NativeMethods.ts_node_type(node)) ?? "<null>";

    public static IEnumerable<TSNode> Children(TSNode node)
    {
        var count = NativeMethods.ts_node_child_count(node);
        for (uint i = 0; i < count; i++)
        {
            yield return NativeMethods.ts_node_child(node, i);
        }
    }

    public static IEnumerable<TSNode> DescendantsAndSelf(TSNode node)
    {
        yield return node;
        foreach (var child in Children(node))
        {
            foreach (var descendant in DescendantsAndSelf(child))
            {
                yield return descendant;
            }
        }
    }

    public static bool HasErrorDescendant(TSNode node) =>
        DescendantsAndSelf(node).Any(n => NativeMethods.ts_node_is_error(n) || NativeMethods.ts_node_is_missing(n));

    public static TSNode? FindDescendant(TSNode node, Func<TSNode, bool> predicate)
    {
        foreach (var candidate in DescendantsAndSelf(node))
        {
            if (predicate(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
