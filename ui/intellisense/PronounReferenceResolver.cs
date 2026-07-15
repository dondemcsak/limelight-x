using System.Runtime.InteropServices;

namespace LimelightX.UI.Intellisense;

/// <summary>
/// Shared "does this pronoun have a preceding sentence to refer to"
/// resolution, used by both HoverService (§7.2 pronoun hover,
/// best-effort/never authoritative) and DiagnosticService (bdd-ui-interactions.md
/// §2.28's dangling-pronoun diagnostic) - both need the exact same
/// nearest-preceding-sentence check, just for different purposes (a hover
/// message vs. a diagnostic).
/// </summary>
public static class PronounReferenceResolver
{
    /// <summary>The pronoun's nearest preceding top-level sentence, or null if there is none to refer to.</summary>
    public static TSNode? PrecedingSentence(TSNode pronoun)
    {
        if (EnclosingSentence(pronoun) is not { } sentence
            || PreviousSibling(sentence) is not { } previous
            || NodeType(previous) != "sentence"
            || NativeMethods.ts_node_child_count(previous) == 0)
        {
            return null;
        }

        return previous;
    }

    private static TSNode? EnclosingSentence(TSNode node)
    {
        while (!NativeMethods.ts_node_is_null(node))
        {
            if (NodeType(node) == "sentence")
            {
                return node;
            }

            node = NativeMethods.ts_node_parent(node);
        }

        return null;
    }

    /// <summary>No ts_node_next/prev_sibling export exists (spec/parsing/tree-sitter-runtime-build-guide.md §2's fixed export list) - walks the parent's children instead.</summary>
    private static TSNode? PreviousSibling(TSNode node)
    {
        var parent = NativeMethods.ts_node_parent(node);
        if (NativeMethods.ts_node_is_null(parent))
        {
            return null;
        }

        var count = NativeMethods.ts_node_child_count(parent);
        var nodeStart = NativeMethods.ts_node_start_byte(node);

        TSNode? previous = null;
        for (uint i = 0; i < count; i++)
        {
            var child = NativeMethods.ts_node_child(parent, i);
            if (NativeMethods.ts_node_start_byte(child) == nodeStart)
            {
                return previous;
            }

            previous = child;
        }

        return null;
    }

    private static string NodeType(TSNode node) =>
        Marshal.PtrToStringUTF8(NativeMethods.ts_node_type(node)) ?? string.Empty;
}
