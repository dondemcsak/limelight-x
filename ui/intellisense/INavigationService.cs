namespace LimelightX.UI.Intellisense;

/// <summary>
/// App-wide singleton (mirrors ICompletionService's pattern). Go to
/// Definition (bdd-ui-interactions.md §2.26) - syntactic, CST-only,
/// best-effort, same boundary as HoverService's variable hover (§7.1):
/// never authoritative, never semantic/normalizer-aware.
/// </summary>
public interface INavigationService
{
    /// <summary>The [Start, End) byte span of the bind_stmt that bound the reference at cursorByte, or null if there is none.</summary>
    (int Start, int End)? FindDefinition(string text, TSNode root, int cursorByte);
}
