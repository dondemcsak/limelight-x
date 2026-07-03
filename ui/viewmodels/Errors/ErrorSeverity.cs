namespace LimelightX.UI.ViewModels.Errors;

/// <summary>
/// ui-error-handling.md §1, ui-viewmodels.md §2. Deserialized case-insensitively
/// from the lowercase wire values ("info"|"warning"|"error"|"fatal") via
/// JsonStringEnumConverter on PipelineJsonOptions.
/// </summary>
public enum ErrorSeverity
{
    Info,
    Warning,
    Error,
    Fatal,
}
