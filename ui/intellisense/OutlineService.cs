using System.Runtime.InteropServices;
using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Intellisense;

/// <summary>
/// Real Tree-sitter-backed implementation (ui-intellisense-implementation-guide.md
/// §8.1). One OutlineItem per top-level `sentence` node's inner statement -
/// stateless, no query needed. Every statement rule in tree-sitter/grammar.js
/// starts with exactly one leading keyword ("Load the", "Extract the",
/// "Summarize", "Translate", "Let", "Rewrite", "Format"), so Verb is always
/// the statement's 0th child. Variable (the bound name) exists only for
/// bind_stmt ("Let" $.name "be" ...), its 1st child. Resource is the
/// statement's primary operand: bind_stmt's bound value (3rd child,
/// resource_from|expression) for bind_stmt, otherwise the 1st child
/// (resource/target/input, depending on statement) - mirrors the same
/// child-index approach HoverService.VariableBindingText already uses for
/// bind_stmt's name child.
/// </summary>
public sealed class OutlineService : IOutlineService
{
    public IEnumerable<OutlineItem> GetOutline(string text, TSNode root)
    {
        var utf8Text = new Utf8Text(text);

        foreach (var sentence in FindAll(root, "sentence"))
        {
            if (NativeMethods.ts_node_child_count(sentence) == 0)
            {
                continue;
            }

            var statement = NativeMethods.ts_node_child(sentence, 0);

            yield return new OutlineItem
            {
                Verb = ExtractVerb(text, utf8Text, statement),
                Resource = ExtractResource(text, utf8Text, statement),
                Variable = ExtractVariable(text, utf8Text, statement),
                Line = (int)NativeMethods.ts_node_start_point(sentence).Row + 1,
            };
        }
    }

    private static string? ExtractVerb(string text, Utf8Text utf8Text, TSNode statement) =>
        NativeMethods.ts_node_child_count(statement) > 0
            ? NodeText(text, utf8Text, NativeMethods.ts_node_child(statement, 0))
            : null;

    private static string? ExtractVariable(string text, Utf8Text utf8Text, TSNode statement) =>
        NodeType(statement) == "bind_stmt" && NativeMethods.ts_node_child_count(statement) > 1
            ? NodeText(text, utf8Text, NativeMethods.ts_node_child(statement, 1))
            : null;

    private static string? ExtractResource(string text, Utf8Text utf8Text, TSNode statement)
    {
        var index = NodeType(statement) == "bind_stmt" ? 3u : 1u;
        return NativeMethods.ts_node_child_count(statement) > index
            ? NodeText(text, utf8Text, NativeMethods.ts_node_child(statement, index))
            : null;
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
