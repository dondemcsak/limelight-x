using System.Runtime.InteropServices;

namespace LimelightX.UI.Intellisense;

/// <summary>
/// Shared "find every bind_stmt in the document" scan, used by HoverService
/// (§7.1 Variable Hover), CompletionService (variable-name completion
/// candidates, bdd-ui-interactions.md §2.20), and NavigationService (Go to
/// Definition, §2.26) - all three need the same "which name was bound where"
/// data, just consume it differently. Syntactic, CST-only, best-effort - a
/// local scan of bind_stmt nodes in the same document, not a semantic lookup
/// (ui-intellisense-engine-spec.md §5.1's note on Variables).
/// </summary>
public static class BindingScanner
{
    public readonly record struct Binding(string Name, TSNode BindStmt, int StartByte);

    /// <summary>Every bind_stmt in the tree with at least a name child (child index 1, "Let" $.name "be" ...), in document order.</summary>
    public static IEnumerable<Binding> FindAllBindings(string text, Utf8Text utf8Text, TSNode root)
    {
        foreach (var bindStmt in FindAll(root, "bind_stmt"))
        {
            if (NativeMethods.ts_node_child_count(bindStmt) < 2)
            {
                continue;
            }

            var nameNode = NativeMethods.ts_node_child(bindStmt, 1);
            yield return new Binding(NodeText(text, utf8Text, nameNode), bindStmt, (int)NativeMethods.ts_node_start_byte(bindStmt));
        }
    }

    private static IEnumerable<TSNode> FindAll(TSNode node, string type)
    {
        if (NodeType(node) == type)
        {
            yield return node;
        }

        var count = NativeMethods.ts_node_child_count(node);
        for (uint i = 0; i < count; i++)
        {
            foreach (var descendant in FindAll(NativeMethods.ts_node_child(node, i), type))
            {
                yield return descendant;
            }
        }
    }

    private static string NodeText(string text, Utf8Text utf8Text, TSNode node)
    {
        var start = utf8Text.ByteOffsetToCharOffset((int)NativeMethods.ts_node_start_byte(node));
        var end = utf8Text.ByteOffsetToCharOffset((int)NativeMethods.ts_node_end_byte(node));
        return text[start..end];
    }

    private static string NodeType(TSNode node) =>
        Marshal.PtrToStringUTF8(NativeMethods.ts_node_type(node)) ?? string.Empty;
}
