using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LimelightX.UI.Routing;
using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Views;

public partial class MainWindow : Window
{
    private readonly HomePage _homePage;
    private readonly EditorPage _editorPage;
    private readonly ExecutionPage _executionPage;
    private readonly SettingsPage _settingsPage;

    public MainWindow()
    {
        InitializeComponent();
        _homePage = new HomePage();
        _editorPage = new EditorPage();
        _executionPage = new ExecutionPage();
        _settingsPage = new SettingsPage();
        DataContextChanged += (_, _) => AttachNavigation();
    }

    public MainWindow(
        NavigationViewModel navigation,
        FileLoaderViewModel fileLoader,
        EditorViewModel editor,
        PipelineExecutionViewModel pipelineExecution,
        SettingsViewModel settings)
        : this()
    {
        DataContext = navigation;
        _homePage.DataContext = fileLoader;
        _homePage.Navigation = navigation;
        _editorPage.DataContext = editor;
        _executionPage.DataContext = pipelineExecution;
        _settingsPage.DataContext = settings;

        // ui-accessibility.md §9: basic keyboard shortcuts. Editor's commands
        // are already CanExecute-gated (Guard 2), and NavigationViewModel's
        // guards apply to Settings the same as sidebar navigation - binding
        // directly to the target commands here doesn't bypass either.
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.R, KeyModifiers.Control), Command = editor.RunCommand });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.E, KeyModifiers.Control), Command = editor.ExplainCommand });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.T, KeyModifiers.Control), Command = editor.TraceCommand });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.S, KeyModifiers.Control), Command = settings.SaveSettingsCommand });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Control), Command = navigation.NavigateToSettingsCommand });
    }

    private void AttachNavigation()
    {
        if (DataContext is NavigationViewModel navigation)
        {
            navigation.PropertyChanged += OnNavigationPropertyChanged;
            SetPage(navigation.CurrentPage);
        }
    }

    private void OnNavigationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NavigationViewModel.CurrentPage) && sender is NavigationViewModel navigation)
        {
            SetPage(navigation.CurrentPage);
        }
    }

    private void SetPage(PageType page)
    {
        var control = page switch
        {
            PageType.Home => (Control)_homePage,
            PageType.Editor => _editorPage,
            PageType.Execution => _executionPage,
            PageType.Settings => _settingsPage,
            _ => _homePage,
        };

        PageHost.Content = control;

        // ui-accessibility.md §3: focus moves to the first interactive element
        // on page navigation.
        Dispatcher.UIThread.Post(() => FocusFirstDescendant(control), DispatcherPriority.Loaded);
    }

    private static void FocusFirstDescendant(Control root)
    {
        foreach (var descendant in root.GetVisualDescendants())
        {
            if (descendant is InputElement { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true } element)
            {
                element.Focus();
                return;
            }
        }
    }
}
