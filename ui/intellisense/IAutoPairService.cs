namespace LimelightX.UI.Intellisense;

/// <summary>
/// App-wide singleton (mirrors ICompletionService's pattern). Determines
/// whether typing an opening quote/brace at the given cursor position should
/// auto-insert its matching closer (bdd-ui-interactions.md §2.24-§2.25) -
/// syntactic, CST-only, same trial-insertion approach CompletionService
/// already uses for "is this grammar-valid here".
/// </summary>
public interface IAutoPairService
{
    bool CanAutoClose(string text, TSNode root, int cursorByte, string opener);
}
