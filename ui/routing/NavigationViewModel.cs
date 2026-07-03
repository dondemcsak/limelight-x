using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LimelightX.UI.Routing;

/// <summary>
/// Controls deterministic navigation between pages (ui-viewmodels.md §3.1,
/// ui-routing-navigation.md §3). No history stack, no deep-linking.
///
/// Guard checks are exposed as delegates rather than direct references to the
/// owning ViewModels (FileLoaderViewModel, EditorViewModel,
/// PipelineExecutionViewModel, SettingsViewModel) so this type has no
/// compile-time dependency on ViewModels that don't exist until later phases.
/// The composition root (App.axaml.cs) wires the delegates once every
/// ViewModel exists (Phase 3: EditorGuard, Phase 5: IsExecutionBusy, Phase 6:
/// SettingsLeaveGuard + IsFirstRunSetupRequired).
/// </summary>
public partial class NavigationViewModel : ObservableObject
{
    [ObservableProperty]
    private PageType _currentPage = PageType.Home;

    /// <summary>
    /// Guard 4 (ui-routing-navigation.md §4): no navigation during
    /// IsRunning/IsTracing/IsExplaining. Wired by PipelineExecutionViewModel (Phase 5).
    /// </summary>
    public Func<bool> IsExecutionBusy { get; set; } = () => false;

    /// <summary>
    /// Guard 1: Home -> Editor requires FileLoaderViewModel.FileContent != null.
    /// Wired in Phase 3.
    /// </summary>
    public Func<NavigationGuardResult> EditorGuard { get; set; } = () => NavigationGuardResult.Allowed();

    /// <summary>
    /// Guards 2+3: direct sidebar navigation to Execution requires valid CNL and a
    /// prior successful backend response (ui-routing-navigation.md §9 - "Sidebar
    /// cannot navigate to ExecutionPage unless pipeline succeeded"). Automatic
    /// post-pipeline navigation from RunPipelineCommand/etc. bypasses this check
    /// since the backend call it just made already satisfies it. Wired in Phase 5.
    /// </summary>
    public Func<NavigationGuardResult> ExecutionGuard { get; set; } = () => NavigationGuardResult.Allowed();

    /// <summary>
    /// Guard 5: leaving SettingsPage with unsaved changes. Async because it may
    /// show a Stay/Discard confirmation modal and await the user's choice.
    /// Returns true if navigation should proceed. Wired in Phase 6.
    /// </summary>
    public Func<Task<bool>> SettingsLeaveGuard { get; set; } = () => Task.FromResult(true);

    /// <summary>
    /// True when llx serve failed to start (missing/invalid config or API key) -
    /// Home/Editor/Execution become unreachable until SaveSettingsCommand succeeds
    /// (ui-routing-navigation.md §2). Wired in Phase 6.
    /// </summary>
    [ObservableProperty]
    private bool _isFirstRunSetupRequired;

    /// <summary>
    /// Raised when a guard blocks navigation, carrying the human-readable reason
    /// for the "Navigation Blocked" modal (ui-routing-navigation.md §4). Consumed
    /// by ModalService once it exists (Phase 3 stub, Phase 7 full).
    /// </summary>
    public event Action<string>? NavigationBlocked;

    [RelayCommand]
    private Task NavigateToHomeAsync() => TryNavigateAsync(PageType.Home);

    [RelayCommand]
    private Task NavigateToEditorAsync()
    {
        var guard = EditorGuard();
        if (!guard.IsAllowed)
        {
            NavigationBlocked?.Invoke(guard.Reason ?? "Navigation blocked.");
            return Task.CompletedTask;
        }

        return TryNavigateAsync(PageType.Editor);
    }

    [RelayCommand]
    private Task NavigateToExecutionAsync()
    {
        var guard = ExecutionGuard();
        if (!guard.IsAllowed)
        {
            NavigationBlocked?.Invoke(guard.Reason ?? "Navigation blocked.");
            return Task.CompletedTask;
        }

        return TryNavigateAsync(PageType.Execution);
    }

    private PageType _pageBeforeSettings = PageType.Home;

    [RelayCommand]
    private Task NavigateToSettingsAsync()
    {
        if (CurrentPage != PageType.Settings)
        {
            _pageBeforeSettings = CurrentPage;
        }

        return TryNavigateAsync(PageType.Settings);
    }

    /// <summary>
    /// Navigates without re-running per-target guards - used for automatic
    /// post-pipeline navigation to ExecutionPage, which is already validated by
    /// the backend call that triggered it (ui-routing-navigation.md §5).
    /// </summary>
    public Task NavigateDirectAsync(PageType target) => TryNavigateAsync(target);

    /// <summary>
    /// Returns to whichever page was active before Settings was entered (or
    /// Home, if none was recorded - e.g. the first-launch flow). This is the
    /// one deliberate exception to "no history stack": a single
    /// remembered page, not a stack, used only by SettingsViewModel's
    /// Save/Cancel commands (ui-viewmodels.md §3.3).
    /// </summary>
    public Task NavigateBackFromSettingsAsync() => TryNavigateAsync(_pageBeforeSettings);

    private async Task TryNavigateAsync(PageType target)
    {
        if (IsExecutionBusy())
        {
            NavigationBlocked?.Invoke("Cannot navigate while a pipeline operation is running.");
            return;
        }

        // ui-routing-navigation.md §2: Home/Editor/Execution stay unreachable
        // until Settings has been saved successfully during first-run/broken-config
        // startup. Centralized here (not per-command) so it applies uniformly,
        // including to NavigateDirectAsync's automatic post-pipeline navigation.
        if (IsFirstRunSetupRequired && target != PageType.Settings)
        {
            NavigationBlocked?.Invoke("Settings must be configured before continuing.");
            return;
        }

        if (CurrentPage == PageType.Settings && target != PageType.Settings)
        {
            var canLeave = await SettingsLeaveGuard();
            if (!canLeave)
            {
                return;
            }
        }

        CurrentPage = target;
    }
}
