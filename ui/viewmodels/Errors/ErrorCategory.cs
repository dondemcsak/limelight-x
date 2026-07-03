namespace LimelightX.UI.ViewModels.Errors;

/// <summary>
/// ui-error-handling.md §1, ui-viewmodels.md §2. Deserialized case-insensitively
/// from the lowercase wire values via JsonStringEnumConverter on PipelineJsonOptions.
/// </summary>
public enum ErrorCategory
{
    Validation,
    Pipeline,
    Api,
    Rendering,
    Navigation,
    Editor,
    State,
}
