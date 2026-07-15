namespace LimelightX.UI.ViewModels.Inspectors;

/// <summary>
/// ui-viewmodels.md §6.6. Distinct from Services.Dto.ResultContentType (the
/// wire enum Plain/Markdown/Json) - this is the ViewModel-facing name spec
/// gives it; FinalResultViewModel maps one to the other.
/// </summary>
public enum ResultContentType
{
    PlainText,
    Markdown,
    Json,
}
