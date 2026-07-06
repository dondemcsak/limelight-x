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

    /// <summary>Design-time/XAML-runtime-loader constructor only (AVLN3001) - the composition root always uses the constructor below.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(WorkspaceViewModel workspace, SettingsViewModel settings)
        : this()
    {
        DataContext = workspace;
        SettingsModal.DataContext = settings;

        // Ctrl+S only matters while the Settings modal is open; SettingsViewModel
        // is a single composition-root instance (ui-viewmodels.md §9), so a
        // fixed command reference here is safe (unlike Run/Explain, which must
        // track whichever tab is active - see the XAML KeyBindings).
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.S, KeyModifiers.Control), Command = settings.SaveSettingsCommand });

        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);

        // ui-accessibility.md §3: focus moves to the first interactive element
        // after switching tabs or opening/closing Settings.
        workspace.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(WorkspaceViewModel.ActiveTab) or nameof(WorkspaceViewModel.IsSettingsOpen))
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
            if (e.Key == Key.O && e.KeyModifiers == KeyModifiers.Control && DataContext is WorkspaceViewModel workspace)
            {
                e.Handled = true;
                workspace.OpenFolderCommand.Execute(null);
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
