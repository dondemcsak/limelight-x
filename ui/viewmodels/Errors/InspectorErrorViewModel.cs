namespace LimelightX.UI.ViewModels.Errors;

/// <summary>
/// Backs InspectorErrorPanel (ui-components.md §7.2, ui-error-handling.md
/// §9.3). Computed on the fly from the first error in an inspector's own
/// Errors collection - inspectors already track HasErrors/Errors directly
/// (ui-viewmodels.md §10); this is just a display-shaped projection of that
/// state, not additional persisted state.
/// </summary>
public sealed class InspectorErrorViewModel
{
    public required string Message { get; init; }

    public required ErrorSeverity Severity { get; init; }

    public static InspectorErrorViewModel? FromErrors(IReadOnlyList<UiError> errors)
    {
        if (errors.Count == 0)
        {
            return null;
        }

        var first = errors[0];
        return new InspectorErrorViewModel { Message = first.Message, Severity = first.Severity };
    }
}
