namespace LimelightX.UI.Routing;

/// <summary>
/// Deterministic page set (ui-routing-navigation.md §1). No URL routing,
/// no history stack - pages switch purely via NavigationViewModel.CurrentPage.
/// </summary>
public enum PageType
{
    Home,
    Editor,
    Execution,
    Settings,
}
