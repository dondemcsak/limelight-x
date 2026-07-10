using System.Runtime.InteropServices;
using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Intellisense;

/// <summary>
/// Real Tree-sitter-backed implementation. App-wide singleton, owning a
/// private ParserHost dedicated to trial-insertion reparses (mirrors
/// CnlSyntaxColorizer's precedent of owning a small private ParserHost
/// rather than sharing the tab's one) - safe to share across every tab's
/// completion requests since Avalonia is single-threaded and each Parse call
/// is a fresh, independent full reparse.
///
/// A single CST alone doesn't give enough signal for "what tokens are valid
/// here" at a mid-edit cursor position - the grammar's own error recovery
/// often produces one large ERROR node with no recognizable substructure
/// around an incomplete construct (spec/parsing/tree-sitter-runtime-build-guide.md
/// §6's fourth finding). Instead, this tries every candidate keyword/pronoun
/// literal (ui-intellisense-engine-spec.md §5.1's "Verbs"/"Keywords"/
/// "Pronouns" sources - "Variables"/"Prompt templates" are out of scope,
/// nothing exercises them), splices it in at the cursor, reparses, and keeps
/// it only if the resulting tree contains a node of that exact type starting
/// exactly at the cursor byte - i.e. the candidate was recognized as its own
/// grammar token, not swallowed into a free-text word. Confirmed empirically
/// (tree-sitter-runtime-build-guide.md §6): inserting "from" after
/// "Load the article " produces a real `resource`+`from` node pair; a
/// non-grammar word like "xyz" produces neither.
/// </summary>
public sealed class CompletionService : ICompletionService
{
    // ui-intellisense-engine-spec.md §5.1 "Verbs"/"Keywords" - the literal
    // tokens from tree-sitter/grammar.js's sentence/using_prompt rules.
    private static readonly string[] Keywords =
    [
        "Load the", "Extract the", "Summarize", "Translate", "Let", "Rewrite", "Format",
        "from", "using", "to", "as", "be",
    ];

    // ui-intellisense-engine-spec.md §5.1 "Pronouns".
    private static readonly string[] Pronouns =
    [
        "it", "them", "the result", "the output", "this", "that",
    ];

    // bdd-ui-interactions.md §2.13: empty inside these four free-text node kinds.
    private static readonly HashSet<string> FreeTextNodeTypes =
        ["resource", "target", "format_target", "language"];

    private readonly ParserHost _parserHost = new();

    public IEnumerable<CompletionItem> GetCompletions(string text, TSNode root, int cursorByte)
    {
        if (IsInsideFreeTextPosition(root, cursorByte))
        {
            yield break;
        }

        var utf8Text = new Utf8Text(text);
        var cursorChar = utf8Text.ByteOffsetToCharOffset(cursorByte);

        foreach (var candidate in Keywords.Concat(Pronouns))
        {
            if (IsValidHere(text, cursorChar, cursorByte, candidate))
            {
                yield return new CompletionItem { Text = candidate };
            }
        }
    }

    private static bool IsInsideFreeTextPosition(TSNode root, int cursorByte)
    {
        var node = NativeMethods.ts_node_descendant_for_byte_range(root, (uint)cursorByte, (uint)cursorByte);

        while (!NativeMethods.ts_node_is_null(node))
        {
            if (FreeTextNodeTypes.Contains(NodeType(node)))
            {
                return true;
            }

            node = NativeMethods.ts_node_parent(node);
        }

        return false;
    }

    private bool IsValidHere(string text, int cursorChar, int cursorByte, string candidate)
    {
        var trialText = text[..cursorChar] + candidate + text[cursorChar..];
        var trialRoot = _parserHost.Parse(trialText);

        return HasDescendant(trialRoot, n =>
            (int)NativeMethods.ts_node_start_byte(n) == cursorByte && NodeType(n) == candidate);
    }

    private static bool HasDescendant(TSNode node, Func<TSNode, bool> predicate)
    {
        if (predicate(node))
        {
            return true;
        }

        var count = NativeMethods.ts_node_child_count(node);
        for (uint i = 0; i < count; i++)
        {
            if (HasDescendant(NativeMethods.ts_node_child(node, i), predicate))
            {
                return true;
            }
        }

        return false;
    }

    private static string NodeType(TSNode node) =>
        Marshal.PtrToStringUTF8(NativeMethods.ts_node_type(node)) ?? string.Empty;
}
