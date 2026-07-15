using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LimelightX.UI.ViewModels;
using LimelightX.UI.ViewModels.Workspace;

namespace LimelightX.UI.Views;

public partial class MainWindow : Window
{
    // Ctrl+K, Ctrl+O chord (ui-accessibility.md §9: Open Folder) - VS-Code-style:
    // Ctrl+K arms a short pending window; Ctrl+O within it opens the folder
    // picker, anything else (or the timeout) cancels silently. Avalonia's
    // KeyBinding/KeyGesture has no multi-key chord primitive, so this listens
    // at the Window's Tunnel routing stage to see the keys before whichever
    // control is focused (e.g. the CNL editor) would otherwise consume them.
    private static readonly TimeSpan ChordTimeout = TimeSpan.FromMilliseconds(1500);

    private DispatcherTimer? _chordTimer;
    private bool _chordArmed;
    private SettingsViewModel? _settings;

    /// <summary>Design-time/XAML-runtime-loader constructor only (AVLN3001) - the composition root always uses the constructor below.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(WorkspaceViewModel workspace, SettingsViewModel settings, AboutViewModel about)
        : this()
    {
        DataContext = workspace;
        SettingsModal.DataContext = settings;
        AboutModal.DataContext = about;
        _settings = settings;

        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);

        // ui-accessibility.md §3: focus moves to the first interactive element
        // after switching tabs or opening/closing Settings/About.
        workspace.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(WorkspaceViewModel.ActiveTab) or nameof(WorkspaceViewModel.IsSettingsOpen) or nameof(WorkspaceViewModel.IsAboutOpen))
            {
                Dispatcher.UIThread.Post(() => FocusFirstDescendant(this), DispatcherPriority.Loaded);
            }
        };
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (_chordArmed)
        {
            DisarmChord();
            if (DataContext is not WorkspaceViewModel workspace)
            {
                return;
            }

            if (e.Key == Key.O && e.KeyModifiers == KeyModifiers.Control)
            {
                e.Handled = true;
                workspace.OpenFolderCommand.Execute(null);
            }
            else if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.None)
            {
                e.Handled = true;
                workspace.SaveAllCommand.Execute(null);
            }

            return;
        }

        if (e.Key == Key.K && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            _chordArmed = true;
            _chordTimer = new DispatcherTimer { Interval = ChordTimeout };
            _chordTimer.Tick += (_, _) => DisarmChord();
            _chordTimer.Start();
            return;
        }

        // Plain Ctrl+S resolves between two different commands depending on
        // whether the Settings modal is open (ui-accessibility.md §9) -
        // SettingsViewModel is a single composition-root instance
        // (ui-viewmodels.md §9), so a fixed reference here is safe (unlike
        // Run/Explain, which must track whichever tab is active - see the
        // XAML KeyBindings).
        if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control && DataContext is WorkspaceViewModel ws)
        {
            e.Handled = true;
            if (ws.IsSettingsOpen)
            {
                _settings?.SaveSettingsCommand.Execute(null);
            }
            else
            {
                ws.SaveCommand.Execute(null);
            }
        }
    }

    private void DisarmChord()
    {
        _chordTimer?.Stop();
        _chordTimer = null;
        _chordArmed = false;
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
