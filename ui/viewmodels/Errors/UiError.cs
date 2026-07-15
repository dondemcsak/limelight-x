namespace LimelightX.UI.ViewModels.Errors;

/// <summary>
/// Canonical error shape (ui-error-handling.md §1, ui-viewmodels.md §2,
/// ui-data-contracts.md §1). Code/Category are populated directly from the
/// wire response's code/category fields with no client-side derivation.
///
/// Code is intentionally a plain string, never an enum: the server may add
/// new codes without a spec update (e.g. ERR_IR_COMPILE, confirmed present
/// in the Rust implementation but not yet listed in api.md §10's table) and
/// the UI must not fail to deserialize or switch exhaustively over it - it
/// only needs Code for display and Category/Severity to pick an error surface.
/// </summary>
public class UiError
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public required ErrorSeverity Severity { get; init; }

    public required ErrorCategory Category { get; init; }

    public ErrorLocation? Location { get; init; }
}
