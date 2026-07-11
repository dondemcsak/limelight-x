using LimelightX.UI.ViewModels.Editor;

namespace LimelightX.UI.ViewModels.Errors;

/// <summary>Domain-tagged subtype for Category:Validation errors (ui-viewmodels.md §2).</summary>
public sealed class ValidationError : UiError
{
    /// <summary>
    /// Client-side presentation classification for CNL parse errors
    /// (ui-error-handling.md §6.2) - see <see cref="CnlErrorKind"/>. Defaults
    /// to Parser for non-CNL validation errors (e.g. Settings field
    /// validation), which never populate this.
    /// </summary>
    public CnlErrorKind Kind { get; init; } = CnlErrorKind.Parser;
}
