namespace LimelightX.UI.ViewModels;

/// <summary>
/// One entry in EditorViewModel.QuickFixes (ui-viewmodels.md §6) - built from
/// a LocalDiagnostic that carries a non-null SuggestedFix. InsertionByte is
/// the diagnostic's StartByte (the fix's insertion point); InsertText is the
/// missing literal. Also reused as EditorViewModel.GhostSuggestion's shape
/// (bdd-ui-interactions.md §2.18) - the same item drives both the popup
/// quick-fix path and Tab-to-accept ghost text (§2.19), applied by
/// ApplyQuickFixCommand.
/// </summary>
public sealed class QuickFixItem
{
    public required string Title { get; init; }
    public required int InsertionByte { get; init; }
    public required string InsertText { get; init; }
}
