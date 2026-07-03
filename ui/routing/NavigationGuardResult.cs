namespace LimelightX.UI.Routing;

/// <summary>
/// Result of a navigation guard check (ui-routing-navigation.md §4). Guard
/// failures (1-4) surface a "Navigation Blocked" modal with Reason as the
/// message; Guard 5 (unsaved Settings) uses a distinct confirmation modal
/// handled separately by SettingsViewModel/NavigationViewModel, not this type.
/// </summary>
public sealed class NavigationGuardResult
{
    private NavigationGuardResult(bool isAllowed, string? reason)
    {
        IsAllowed = isAllowed;
        Reason = reason;
    }

    public bool IsAllowed { get; }

    public string? Reason { get; }

    public static NavigationGuardResult Allowed() => new(true, null);

    public static NavigationGuardResult Blocked(string reason) => new(false, reason);
}
