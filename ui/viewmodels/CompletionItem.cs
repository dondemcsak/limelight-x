namespace LimelightX.UI.ViewModels;

/// <summary>One entry for EditorViewModel.CompletionItems (ui-viewmodels.md §4.1), returned directly by ICompletionService.GetCompletions - no separate CompletionResult wrapper type.</summary>
public sealed class CompletionItem
{
    public required string Text { get; init; }

    public string? Description { get; init; }

    /// <summary>Ranking category (ui-intellisense-engine-spec.md §5.3, bdd-ui-interactions.md §2.21) - defaults to Keyword, the same default every pre-existing call site (structural keywords) already implies.</summary>
    public CompletionKind Kind { get; init; } = CompletionKind.Keyword;

    /// <summary>
    /// Chars already typed (immediately before the cursor) that this item's
    /// match consumed (bdd-ui-interactions.md §2.30, §2.34) - 0 for the
    /// original "suggest at an empty boundary" case (§2.12). CnlCompletionData.Complete()
    /// uses this to replace exactly the already-typed prefix instead of
    /// just inserting at the caret, so accepting "Summarize" while
    /// "Summariz" is on screen produces "Summarize", not "SummarizSummarize".
    /// </summary>
    public int PrefixLength { get; init; }

    /// <summary>
    /// Full sentence skeleton to insert instead of Text when set
    /// (bdd-ui-interactions.md §2.23) - e.g. accepting the "Load the" verb
    /// completion inserts `Load the  from "".` instead of just `Load the`.
    /// Null for every non-verb candidate, which still just inserts Text.
    /// </summary>
    public string? SnippetText { get; init; }

    /// <summary>Char offset within SnippetText to place the caret at after insertion (e.g. right before "from") - ignored when SnippetText is null.</summary>
    public int? SnippetCursorOffset { get; init; }
}
