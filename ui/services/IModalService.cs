namespace LimelightX.UI.Services;

/// <summary>
/// Modal dialog surface (ui-error-handling.md §5, ui-routing-navigation.md
/// §4). Phase 7 formalizes the full severity-driven error taxonomy (ARIA
/// alert semantics, etc.) on top of this service.
/// </summary>
public interface IModalService
{
    /// <summary>Shows the {Title:"Navigation Blocked", Buttons:[OK]} dialog and awaits acknowledgment.</summary>
    Task ShowBlockedNavigationAsync(string reason);

    /// <summary>
    /// Guard 5's distinct confirmation (ui-routing-navigation.md §4):
    /// {Title:"Unsaved Changes", Buttons:[Stay, Discard Changes]}. Returns
    /// true if the user chose Discard Changes, false if Stay.
    /// </summary>
    Task<bool> ShowUnsavedChangesConfirmationAsync();

    /// <summary>Category:Api, Severity:Fatal modal (ui-error-handling.md §5) - blocks all actions until acknowledged.</summary>
    Task ShowFatalErrorAsync(string message);
}
