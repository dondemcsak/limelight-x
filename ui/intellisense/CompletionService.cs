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
/// §6's fourth finding). Instead, this tries every candidate - verbs,
/// keywords, pronouns, bound variable names, and a prompt-template skeleton
/// (ui-intellisense-engine-spec.md §5.1's five sources) - splices its
/// still-untyped remainder in at the cursor (MatchPrefix below -
/// bdd-ui-interactions.md §2.30-§2.31), reparses, and keeps it only if the
/// resulting tree contains a matching node starting exactly where the
/// already-typed prefix began - i.e. the candidate was recognized as its own
/// grammar token, not swallowed into a free-text word. Confirmed empirically
/// (tree-sitter-runtime-build-guide.md §6): inserting "from" after
/// "Load the article " produces a real `resource`+`from` node pair; a
/// non-grammar word like "xyz" produces neither. Results are ranked per
/// §5.3 (Variable, Pronoun, Verb, Keyword, PromptTemplate, in that order -
/// CompletionKind's own declared enum order).
/// </summary>
public sealed class CompletionService : ICompletionService
{
    // ui-intellisense-engine-spec.md §5.1 "Verbs" - the leading keyword of each of the seven statement rules.
    private static readonly string[] Verbs =
    [
        "Load the", "Extract the", "Summarize", "Translate", "Let", "Rewrite", "Format",
    ];

    // ui-intellisense-engine-spec.md §5.1 "Keywords" - structural, non-verb literal tokens.
    private static readonly string[] Keywords =
    [
        "from", "using", "to", "as", "be",
    ];

    // ui-intellisense-engine-spec.md §5.1 "Pronouns".
    private static readonly string[] Pronouns =
    [
        "it", "them", "the result", "the output", "this", "that",
    ];

    // ui-intellisense-engine-spec.md §5.1 "Prompt templates" - structural skeleton only, no content suggestions (bdd-ui-interactions.md §2.22).
    private const string PromptTemplateSkeleton = "{{ prompt: \"\" }}";

    // bdd-ui-interactions.md §2.23: selecting a verb inserts the full sentence
    // skeleton, cursor left just before the given keyword/period (the first
    // blank). One skeleton per verb - the grammar's own seven statement rules.
    private static readonly Dictionary<string, (string Snippet, string CursorBefore)> VerbSnippets = new()
    {
        ["Load the"] = ("Load the  from \"\".", "from"),
        ["Extract the"] = ("Extract the  from .", "from"),
        ["Summarize"] = ("Summarize .", "."),
        ["Translate"] = ("Translate  to .", "to"),
        ["Let"] = ("Let  be .", "be"),
        ["Rewrite"] = ("Rewrite .", "."),
        ["Format"] = ("Format  as .", "as"),
    };

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
        var items = new List<CompletionItem>();

        foreach (var (candidate, kind) in Verbs.Select(v => (v, CompletionKind.Verb))
            .Concat(Keywords.Select(k => (k, CompletionKind.Keyword)))
            .Concat(Pronouns.Select(p => (p, CompletionKind.Pronoun))))
        {
            var (matched, prefixLen) = MatchPrefix(text, cursorChar, candidate);
            if (!matched || !IsValidHere(text, utf8Text, cursorChar, prefixLen, candidate))
            {
                continue;
            }

            if (kind == CompletionKind.Verb && VerbSnippets.TryGetValue(candidate, out var snippet))
            {
                items.Add(new CompletionItem
                {
                    Text = candidate,
                    PrefixLength = prefixLen,
                    Kind = kind,
                    SnippetText = snippet.Snippet,
                    SnippetCursorOffset = snippet.Snippet.IndexOf(snippet.CursorBefore, StringComparison.Ordinal),
                });
            }
            else
            {
                items.Add(new CompletionItem { Text = candidate, PrefixLength = prefixLen, Kind = kind });
            }
        }

        // bdd-ui-interactions.md §2.20: bound variable names, never one bound
        // after the cursor. Distinct, since re-binding the same name twice
        // (e.g. two "Let article be ..." sentences) should suggest it once.
        foreach (var variableName in BindingScanner.FindAllBindings(text, utf8Text, root)
            .Where(b => b.StartByte < cursorByte)
            .Select(b => b.Name)
            .Distinct())
        {
            var (matched, prefixLen) = MatchPrefix(text, cursorChar, variableName);
            if (matched && IsValidVariableHere(text, utf8Text, cursorChar, prefixLen, variableName))
            {
                items.Add(new CompletionItem { Text = variableName, PrefixLength = prefixLen, Kind = CompletionKind.Variable });
            }
        }

        var (promptMatched, promptPrefixLen) = MatchPrefix(text, cursorChar, PromptTemplateSkeleton);
        if (promptMatched && IsValidPromptTemplateHere(text, utf8Text, cursorChar, promptPrefixLen))
        {
            items.Add(new CompletionItem { Text = PromptTemplateSkeleton, PrefixLength = promptPrefixLen, Kind = CompletionKind.PromptTemplate });
        }

        foreach (var item in items.OrderBy(i => (int)i.Kind))
        {
            yield return item;
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

    /// <summary>
    /// Finds the longest already-typed run ending at the cursor that is
    /// also a prefix of candidate, with a genuine word boundary (whitespace
    /// or document start) immediately before it - not just the last N
    /// characters (bdd-ui-interactions.md §2.30). Walking k from the
    /// candidate's own length downward, rather than testing one fixed-length
    /// window, is what makes this correct for BOTH single-word candidates
    /// (unrelated text before the typed word is naturally excluded once a
    /// shorter k finds the real boundary) AND multi-word candidates like
    /// "Load the" (the internal space is only crossed because it's part of
    /// the candidate's own text at that exact relative position, not because
    /// any space is treated as crossable). Ordinal comparison makes this
    /// case-sensitive for free (§2.31).
    /// </summary>
    private static (bool Matched, int PrefixLen) MatchPrefix(string text, int cursorChar, string candidate)
    {
        var maxK = Math.Min(candidate.Length, cursorChar);
        for (var k = maxK; k >= 1; k--)
        {
            var windowStart = cursorChar - k;
            if (text.AsSpan(windowStart, k).SequenceEqual(candidate.AsSpan(0, k))
                && (windowStart == 0 || char.IsWhiteSpace(text[windowStart - 1])))
            {
                return (true, k);
            }
        }

        // Nothing typed yet for the candidate - only a valid (unfiltered)
        // suggestion if the cursor itself sits at a boundary (§2.12's
        // original empty-position case); otherwise the typed text diverged
        // from every prefix of this candidate and it's simply not a match.
        return (cursorChar == 0 || char.IsWhiteSpace(text[cursorChar - 1]), 0);
    }

    /// <summary>
    /// Finding a same-typed, same-position node isn't sufficient on its own
    /// once trial texts get fragmentary (short prefixes mid-typed, e.g.
    /// "Load this" for "Load th" + "this"'s remainder "is") - Tree-sitter's
    /// GLR error recovery will still tokenize a lexically-valid embedded
    /// literal (a real `this` pronoun node) even while wrapping the
    /// surrounding nonsense in an ERROR node, and empirically (confirmed by
    /// dumping the actual trees) it does this for EVERY incomplete sentence,
    /// not just spurious ones: "Load the article from" (a genuine partial
    /// load_stmt) and "Load this" (nonsense) both produce
    /// `program -> ERROR -> [...]`, so an ERROR-ancestor check alone can't
    /// tell them apart - both shapes have one. The real distinguishing
    /// signal is HasCleanPrecedingContext below: in the genuine case, the
    /// ERROR's children are "Load the", "resource", "from" with only
    /// whitespace between them (the parser consumed everything in order); in
    /// the spurious case, "this" is the ERROR's only child but doesn't start
    /// until byte 5, leaving "Load " (real, unconsumed content, not just
    /// whitespace) silently skipped before it.
    /// </summary>
    private bool IsValidHere(string text, Utf8Text utf8Text, int cursorChar, int prefixLen, string candidate)
    {
        var remainder = candidate[prefixLen..];
        var trialText = text[..cursorChar] + remainder + text[cursorChar..];
        var trialRoot = _parserHost.Parse(trialText);
        var wordStartByte = utf8Text.CharOffsetToByteOffset(cursorChar - prefixLen);

        var match = FindDescendant(trialRoot, n =>
            (int)NativeMethods.ts_node_start_byte(n) == wordStartByte && NodeType(n) == candidate);

        return match is { } node && HasCleanPrecedingContext(node, trialText);
    }

    /// <summary>
    /// Same trial-insertion shape as IsValidHere, but a variable candidate's
    /// matching node isn't found by exact type-equals-text like a keyword
    /// literal - grammar/tree-sitter-runtime-build-guide.md §6's fifth
    /// finding means a variable reference at an `input` position parses as
    /// `resource` (free text), not `name`, so this accepts either type and
    /// checks the node's own TEXT against the variable name instead
    /// (mirrors HoverService.VariableBindingText's same dual check).
    /// </summary>
    private bool IsValidVariableHere(string text, Utf8Text utf8Text, int cursorChar, int prefixLen, string variableName)
    {
        var remainder = variableName[prefixLen..];
        var trialText = text[..cursorChar] + remainder + text[cursorChar..];
        var trialRoot = _parserHost.Parse(trialText);
        var wordStartByte = utf8Text.CharOffsetToByteOffset(cursorChar - prefixLen);
        var trialUtf8Text = new Utf8Text(trialText);

        var match = FindDescendant(trialRoot, n =>
            (int)NativeMethods.ts_node_start_byte(n) == wordStartByte
            && (NodeType(n) == "resource" || NodeType(n) == "name")
            && NodeText(trialText, trialUtf8Text, n) == variableName);

        return match is { } node && HasCleanPrecedingContext(node, trialText);
    }

    /// <summary>
    /// Same trial-insertion shape as IsValidHere, but the matched node type
    /// (prompt_hole) is fixed rather than equal to the candidate's own text,
    /// since the candidate is a structural skeleton (`{{ prompt: "" }}`),
    /// not a single literal token.
    /// </summary>
    private bool IsValidPromptTemplateHere(string text, Utf8Text utf8Text, int cursorChar, int prefixLen)
    {
        var remainder = PromptTemplateSkeleton[prefixLen..];
        var trialText = text[..cursorChar] + remainder + text[cursorChar..];
        var trialRoot = _parserHost.Parse(trialText);
        var wordStartByte = utf8Text.CharOffsetToByteOffset(cursorChar - prefixLen);

        var match = FindDescendant(trialRoot, n =>
            (int)NativeMethods.ts_node_start_byte(n) == wordStartByte && NodeType(n) == "prompt_hole");

        return match is { } node && HasCleanPrecedingContext(node, trialText);
    }

    private static string NodeText(string text, Utf8Text utf8Text, TSNode node)
    {
        var start = utf8Text.ByteOffsetToCharOffset((int)NativeMethods.ts_node_start_byte(node));
        var end = utf8Text.ByteOffsetToCharOffset((int)NativeMethods.ts_node_end_byte(node));
        return text[start..end];
    }

    /// <summary>
    /// True if nothing but whitespace sits between this node and whatever
    /// immediately preceded it (its previous sibling, or its parent's own
    /// start if it's the first child) - i.e. the parser didn't have to
    /// silently skip over unconsumed content to reach it.
    /// </summary>
    private static bool HasCleanPrecedingContext(TSNode node, string trialText)
    {
        var parent = NativeMethods.ts_node_parent(node);
        if (NativeMethods.ts_node_is_null(parent))
        {
            return true;
        }

        var nodeStart = NativeMethods.ts_node_start_byte(node);
        var precedingEnd = NativeMethods.ts_node_start_byte(parent);

        var count = NativeMethods.ts_node_child_count(parent);
        for (uint i = 0; i < count; i++)
        {
            var child = NativeMethods.ts_node_child(parent, i);
            if (NativeMethods.ts_node_start_byte(child) == nodeStart)
            {
                break;
            }

            precedingEnd = NativeMethods.ts_node_end_byte(child);
        }

        var trialUtf8Text = new Utf8Text(trialText);
        var start = trialUtf8Text.ByteOffsetToCharOffset((int)precedingEnd);
        var end = trialUtf8Text.ByteOffsetToCharOffset((int)nodeStart);

        return trialText[start..end].All(char.IsWhiteSpace);
    }

    private static TSNode? FindDescendant(TSNode node, Func<TSNode, bool> predicate)
    {
        if (predicate(node))
        {
            return node;
        }

        var count = NativeMethods.ts_node_child_count(node);
        for (uint i = 0; i < count; i++)
        {
            if (FindDescendant(NativeMethods.ts_node_child(node, i), predicate) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static string NodeType(TSNode node) =>
        Marshal.PtrToStringUTF8(NativeMethods.ts_node_type(node)) ?? string.Empty;
}
