using System.Runtime.InteropServices;

namespace LimelightX.UI.Intellisense;

/// <summary>
/// Real Tree-sitter-backed implementation. Resolves the resource/name node
/// at the cursor, then finds the nearest preceding BindingScanner binding
/// whose name matches its text - the exact same algorithm
/// HoverService.VariableBindingText already uses for §7.1 Variable Hover,
/// just returning the bind_stmt's span instead of its source text.
/// </summary>
public sealed class NavigationService : INavigationService
{
    public (int Start, int End)? FindDefinition(string text, TSNode root, int cursorByte)
    {
        if (ResolveReferenceNode(root, cursorByte) is not { } target)
        {
            return null;
        }

        var utf8Text = new Utf8Text(text);
        var targetText = NodeText(text, utf8Text, target);
        var targetStart = (int)NativeMethods.ts_node_start_byte(target);

        (int Start, int End)? best = null;
        var bestStart = -1;

        foreach (var binding in BindingScanner.FindAllBindings(text, utf8Text, root))
        {
            if (binding.StartByte <= targetStart && binding.StartByte > bestStart && binding.Name == targetText)
            {
                best = ((int)NativeMethods.ts_node_start_byte(binding.BindStmt), (int)NativeMethods.ts_node_end_byte(binding.BindStmt));
                bestStart = binding.StartByte;
            }
        }

        return best;
    }

    /// <summary>ts_node_descendant_for_byte_range returns the DEEPEST matching node - walk up to the nearest resource/name ancestor (mirrors HoverService.ResolveRecognizedAncestor's same pattern).</summary>
    private static TSNode? ResolveReferenceNode(TSNode root, int cursorByte)
    {
        var node = NativeMethods.ts_node_descendant_for_byte_range(root, (uint)cursorByte, (uint)cursorByte);

        while (!NativeMethods.ts_node_is_null(node))
        {
            var type = NodeType(node);
            if (type is "resource" or "name")
            {
                return node;
            }

            node = NativeMethods.ts_node_parent(node);
        }

        return null;
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
