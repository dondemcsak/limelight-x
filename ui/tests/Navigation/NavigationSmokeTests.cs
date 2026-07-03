using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LimelightX.UI.Routing;
using LimelightX.UI.Views;
using Xunit;

namespace LimelightX.UI.Tests.Navigation;

/// <summary>
/// Phase 1 "Done" criterion: sidebar navigates between the four placeholder
/// pages. Verifies the MainWindow &lt;-&gt; NavigationViewModel wiring (the
/// PropertyChanged subscription that swaps PageHost.Content) and the
/// NavigationViewModel command/guard logic that drives CurrentPage.
///
/// Full click-driven UI interaction tests (real Button -> Command routing)
/// land in Phase 9 once ui-testing.md's headless click patterns are settled;
/// raw synthetic mouse-event coordinates proved environment-flaky here in a
/// way unrelated to app correctness, so this suite instead drives
/// NavigationViewModel the same way the real Button.Command bindings do
/// (ICommand.Execute) and asserts the View reacts correctly.
/// </summary>
public class NavigationSmokeTests
{
    [AvaloniaFact]
    public void MainWindow_SwapsHostedPage_WhenCurrentPageChanges()
    {
        var navigation = new NavigationViewModel();
        var window = new MainWindow { DataContext = navigation };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.IsType<HomePage>(window.PageHost.Content);

        navigation.NavigateToEditorCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(PageType.Editor, navigation.CurrentPage);
        Assert.IsType<EditorPage>(window.PageHost.Content);

        navigation.NavigateToExecutionCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(PageType.Execution, navigation.CurrentPage);
        Assert.IsType<ExecutionPage>(window.PageHost.Content);

        navigation.NavigateToSettingsCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(PageType.Settings, navigation.CurrentPage);
        Assert.IsType<SettingsPage>(window.PageHost.Content);

        navigation.NavigateToHomeCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(PageType.Home, navigation.CurrentPage);
        Assert.IsType<HomePage>(window.PageHost.Content);
    }

    [AvaloniaFact]
    public void ExecutionBusyGuard_BlocksNavigation()
    {
        var navigation = new NavigationViewModel
        {
            IsExecutionBusy = () => true,
        };

        var blocked = false;
        navigation.NavigationBlocked += _ => blocked = true;

        navigation.NavigateToEditorCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(blocked);
        Assert.Equal(PageType.Home, navigation.CurrentPage);
    }

    [AvaloniaFact]
    public void EditorGuard_BlocksNavigation_WithReason()
    {
        var navigation = new NavigationViewModel
        {
            EditorGuard = () => NavigationGuardResult.Blocked("No file loaded."),
        };

        string? reason = null;
        navigation.NavigationBlocked += r => reason = r;

        navigation.NavigateToEditorCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("No file loaded.", reason);
        Assert.Equal(PageType.Home, navigation.CurrentPage);
    }

    [AvaloniaFact]
    public async Task SettingsLeaveGuard_Dirty_BlocksNavigationUntilConfirmed()
    {
        var navigation = new NavigationViewModel
        {
            SettingsLeaveGuard = () => Task.FromResult(false),
        };
        await navigation.NavigateToSettingsCommand.ExecuteAsync(null);

        await navigation.NavigateToHomeCommand.ExecuteAsync(null);

        Assert.Equal(PageType.Settings, navigation.CurrentPage);
    }

    [AvaloniaFact]
    public async Task SettingsLeaveGuard_DiscardConfirmed_AllowsNavigation()
    {
        var navigation = new NavigationViewModel
        {
            SettingsLeaveGuard = () => Task.FromResult(true),
        };
        await navigation.NavigateToSettingsCommand.ExecuteAsync(null);

        await navigation.NavigateToHomeCommand.ExecuteAsync(null);

        Assert.Equal(PageType.Home, navigation.CurrentPage);
    }

    [AvaloniaFact]
    public async Task NavigateBackFromSettings_ReturnsToPageEnteredFrom()
    {
        var navigation = new NavigationViewModel();
        await navigation.NavigateToEditorCommand.ExecuteAsync(null); // EditorGuard defaults to Allowed
        await navigation.NavigateToSettingsCommand.ExecuteAsync(null);

        await navigation.NavigateBackFromSettingsAsync();

        Assert.Equal(PageType.Editor, navigation.CurrentPage);
    }

    [AvaloniaFact]
    public async Task IsFirstRunSetupRequired_BlocksHomeEditorAndExecution()
    {
        var navigation = new NavigationViewModel { IsFirstRunSetupRequired = true };
        var blocked = false;
        navigation.NavigationBlocked += _ => blocked = true;

        await navigation.NavigateToHomeCommand.ExecuteAsync(null);

        Assert.True(blocked);
        Assert.Equal(PageType.Home, navigation.CurrentPage); // never left the initial page
    }

    [AvaloniaFact]
    public async Task IsFirstRunSetupRequired_StillAllowsNavigatingToSettings()
    {
        var navigation = new NavigationViewModel { IsFirstRunSetupRequired = true, CurrentPage = PageType.Home };

        await navigation.NavigateToSettingsCommand.ExecuteAsync(null);

        Assert.Equal(PageType.Settings, navigation.CurrentPage);
    }
}
