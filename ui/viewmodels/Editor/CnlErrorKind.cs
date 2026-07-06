namespace LimelightX.UI.ViewModels.Editor;

/// <summary>
/// UI-facing presentation kind for an inline CNL validation error
/// (ui-error-handling.md §6.2). All three share one wire code/category
/// (ERR_CNL_PARSE / pipeline) - this only affects which marker/message the
/// editor shows.
/// </summary>
public enum CnlErrorKind
{
    Parser,
    Grammar,
    Hole,
}
