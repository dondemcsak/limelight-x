using System.Runtime.InteropServices;
using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Intellisense;

/// <summary>
/// Real Tree-sitter-backed implementation. bdd-ui-interactions.md §2.11's
/// bare grammar-role labels ("keyword", "pronoun", "resource", "expression
/// hole") remain the fallback for tokens with nothing richer to say; verbs,
/// pronouns with a resolvable preceding sentence, and resource/name nodes
/// matching a known binding get the richer content ui-intellisense-engine-spec.md
/// §7.1-§7.4 describes, per HoverInfo.Position always reflecting the
/// *hovered* node's own span, not the referenced content's.
/// </summary>
public sealed class HoverService : IHoverService
{
    // §7.3 Verb Hover - one line per verb, matching the spec's own "Summarize:
    // Reduce text to a shorter form." example's style/tone.
    private static readonly Dictionary<string, string> VerbDescriptions = new()
    {
        ["Load the"] = "Load: Read a resource from a file.",
        ["Extract the"] = "Extract: Pull specific content from an input.",
        ["Summarize"] = "Summarize: Reduce text to a shorter form.",
        ["Translate"] = "Translate: Convert text into another language.",
        ["Let"] = "Let: Bind a name to a resource or expression.",
        ["Rewrite"] = "Rewrite: Restate text in a different style.",
        ["Format"] = "Format: Convert text into a different structural format.",
    };

    // Non-verb structural keywords - no §7.3-style description exists for these, plain role label.
    private static readonly HashSet<string> StructuralKeywords = ["from", "using", "to", "as", "be"];

    private static readonly HashSet<string> RecognizedRoleTypes =
        ["pronoun", "resource", "name", "target", "format_target", "language", "string", "prompt_hole"];

    // §7.2's own example format: "SummarizeStmt", "LoadStmt", etc.
    private static readonly Dictionary<string, string> StatementDisplayNames = new()
    {
        ["load_stmt"] = "LoadStmt",
        ["extract_stmt"] = "ExtractStmt",
        ["summarize_stmt"] = "SummarizeStmt",
        ["translate_stmt"] = "TranslateStmt",
        ["bind_stmt"] = "BindStmt",
        ["rewrite_stmt"] = "RewriteStmt",
        ["format_stmt"] = "FormatStmt",
    };

    public HoverInfo? GetHover(string text, TSNode root, int cursorByte)
    {
        var node = ResolveRecognizedAncestor(root, cursorByte);
        if (node is not { } target)
        {
            return null;
        }

        var type = NodeType(target);

        if (VerbDescriptions.TryGetValue(type, out var verbDescription))
        {
            return Hover(target, verbDescription);
        }

        return type switch
        {
            _ when StructuralKeywords.Contains(type) => Hover(target, "keyword"),
            "pronoun" => Hover(target, PronounReferenceText(target) ?? "pronoun"),
            "resource" => Hover(target, VariableBindingText(text, root, target) ?? "resource"),
            "name" => Hover(target, VariableBindingText(text, root, target) ?? "variable"),
            "target" => Hover(target, "target"),
            "format_target" => Hover(target, "format target"),
            "language" => Hover(target, "language"),
            "string" => Hover(target, "string"),
            "prompt_hole" => Hover(target, "Expression hole: embeds a literal prompt for the model."),
            _ => null,
        };
    }

    private static HoverInfo Hover(TSNode node, string text) =>
        new() { Text = text, Position = (int)NativeMethods.ts_node_start_byte(node) };

    /// <summary>
    /// ts_node_descendant_for_byte_range returns the DEEPEST matching node -
    /// walk up to the nearest ancestor whose type is one we have hover
    /// content for (mirrors the same fix from bdd-ui-interactions.md §2.11's
    /// pronoun-vs-"it" leaf discovery).
    /// </summary>
    private static TSNode? ResolveRecognizedAncestor(TSNode root, int cursorByte)
    {
        var node = NativeMethods.ts_node_descendant_for_byte_range(root, (uint)cursorByte, (uint)cursorByte);

        while (!NativeMethods.ts_node_is_null(node))
        {
            var type = NodeType(node);
            if (VerbDescriptions.ContainsKey(type) || StructuralKeywords.Contains(type) || RecognizedRoleTypes.Contains(type))
            {
                return node;
            }

            node = NativeMethods.ts_node_parent(node);
        }

        return null;
    }

    /// <summary>
    /// §7.2 Pronoun Hover: the nearest preceding top-level sentence's
    /// statement kind and source line. Null (falls back to plain "pronoun")
    /// when there is no preceding sentence to reference - the same check
    /// DiagnosticService uses to flag a dangling pronoun (bdd-ui-interactions.md
    /// §2.28), shared via PronounReferenceResolver.
    /// </summary>
    private static string? PronounReferenceText(TSNode pronoun)
    {
        if (PronounReferenceResolver.PrecedingSentence(pronoun) is not { } previous)
        {
            return null;
        }

        var statement = NativeMethods.ts_node_child(previous, 0);
        var displayName = StatementDisplayNames.GetValueOrDefault(NodeType(statement), NodeType(statement));
        var line = (int)NativeMethods.ts_node_start_point(previous).Row + 1;

        return $"Pronoun refers to: {displayName} at line {line}";
    }

    /// <summary>
    /// §7.1 Variable Hover: the nearest bind_stmt at or before this node's
    /// position whose bound name's text matches this node's own text.
    /// Matches on text against both `resource`- and `name`-typed nodes
    /// deliberately - grammar/tree-sitter-runtime-build-guide.md §6's fifth
    /// finding means a variable reference at an `input` position parses as
    /// `resource`, not `name`, so this can't key off node type alone.
    /// </summary>
    private static string? VariableBindingText(string text, TSNode root, TSNode node)
    {
        var utf8Text = new Utf8Text(text);
        var nodeText = NodeText(text, utf8Text, node);
        var nodeStart = (int)NativeMethods.ts_node_start_byte(node);

        string? best = null;
        var bestStart = -1;

        foreach (var binding in BindingScanner.FindAllBindings(text, utf8Text, root))
        {
            if (binding.StartByte <= nodeStart && binding.StartByte > bestStart && binding.Name == nodeText)
            {
                best = NodeText(text, utf8Text, binding.BindStmt);
                bestStart = binding.StartByte;
            }
        }

        return best;
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
