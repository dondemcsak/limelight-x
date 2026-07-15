namespace LimelightX.UI.ViewModels.Editor;

/// <summary>
/// UI-facing presentation kind for a ValidationError (ui-error-handling.md
/// §6.2). Parser is the only value in use today - SettingsViewModel's field
/// validation is the sole remaining producer of ValidationError, and never
/// sets Kind explicitly. EditorViewModel no longer constructs
/// ValidationError at all: the editor calls the backend only on an explicit
/// Run/Explain click (cnl-editor-architecture.md §5), and those results
/// surface via PipelineExecutionViewModel.ErrorBanner (plain UiError), not
/// through this type.
/// </summary>
public enum CnlErrorKind
{
    Parser,
}
