using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LimelightX.UI.Services;
using LimelightX.UI.ViewModels.Errors;
using LimelightX.UI.ViewModels.Tabs;

namespace LimelightX.UI.ViewModels.Workspace;

/// <summary>
/// Controls the folder-explorer + tab-strip workspace shell (ui-viewmodels.md
/// §3, ui-routing-navigation.md §3) - replaces the old page-based
/// NavigationViewModel entirely. Never depends on IPipelineService/
/// IEventStreamService directly (ui-routing-navigation.md §10's "Must Not"
/// list) - tab creation is delegated to ITabFactory.
/// </summary>
public partial class WorkspaceViewModel : ObservableObject
{
    private readonly ITabFactory _tabFactory;
    private readonly IFilePickerService _filePicker;
    private readonly IModalService _modal;
    private readonly IExecutionLockService _executionLock;

    public WorkspaceViewModel(ITabFactory tabFactory, IFilePickerService filePicker, IModalService modal, IExecutionLockService executionLock)
    {
        _tabFactory = tabFactory;
        _filePicker = filePicker;
        _modal = modal;
        _executionLock = executionLock;
        _executionLock.ExecutionLockChanged += NotifyExecutionGatedCommandsCanExecuteChanged;

        // FileTreeView binds TreeView.SelectedItem two-way to FileTree.SelectedNode
        // (ui-components.md §3.1) - reacting here, rather than in the View's
        // code-behind, keeps FileTreeView itself free of logic (ui-components.md
        // §1: "Components contain no logic").
        FileTree.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FileTreeViewModel.SelectedNode) && FileTree.SelectedNode is { } node)
            {
                OpenOrFocusTabCommand.Execute(node);
            }
        };

        OpenTabs.CollectionChanged += OnOpenTabsChanged;
    }

    public FileTreeViewModel FileTree { get; } = new();

    [ObservableProperty]
    private string? _rootFolderPath;

    public ObservableCollection<TabViewModel> OpenTabs { get; } = [];

    [ObservableProperty]
    private TabViewModel? _activeTab;

    partial void OnActiveTabChanged(TabViewModel? oldValue, TabViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsActive = false;
        }

        if (newValue is not null)
        {
            newValue.IsActive = true;
        }

        OnPropertyChanged(nameof(ActiveCnlEditor));
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SelectTab(TabViewModel tab) => ActiveTab = tab;

    /// <summary>Stable binding target for global Run/Explain keyboard shortcuts (ui-accessibility.md §9) across tab switches - null when the active tab isn't a .llx tab, or there is none.</summary>
    public EditorViewModel? ActiveCnlEditor => (ActiveTab as CnlTabViewModel)?.Editor;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private bool _isAboutOpen;

    /// <summary>Single shared session-scoped counter for untitled tabs (ui-viewmodels.md §3) - incremented across both New LLX File and New TXT File, never reset per-kind.</summary>
    private int _untitledCounter;

    /// <summary>Filesystem errors surfaced by the file tree (ui-testing.md §11 names this collection "WorkspaceViewModel.Errors").</summary>
    public ObservableCollection<UiError> Errors => FileTree.Errors;

    /// <summary>Raised only when a genuinely new tab is created, not when reopening focuses an existing one - lets the composition root hook per-tab logging (App.axaml.cs, Phase 6).</summary>
    public event Action<TabViewModel>? TabOpened;

    /// <summary>Raised on a successful folder open/restore - lets the composition root persist LastOpenedFolder/RecentFolders (Phase 7) without WorkspaceViewModel depending on IConfigService.</summary>
    public event Action<string>? FolderOpened;

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        var path = await _filePicker.PickFolderAsync();
        if (path is not null)
        {
            OpenRoot(path);
        }
    }

    /// <summary>Opens (or, on startup, restores) a root folder. Public so the composition root can call it directly for restore-on-launch (Phase 7) without going through the file-picker command.</summary>
    public void OpenRoot(string path)
    {
        RootFolderPath = path;
        FileTree.OpenRoot(path);
        FolderOpened?.Invoke(path);
    }

    [RelayCommand]
    private void OpenOrFocusTab(FileTreeNodeViewModel node)
    {
        if (node.IsDirectory)
        {
            return;
        }

        if (TryFocusExisting(node.FullPath))
        {
            return;
        }

        if (TryCreateTab(node.FullPath, out var tab))
        {
            AddNewTab(tab);
        }
    }

    /// <summary>Shared .llx-vs-else dispatch (ui-viewmodels.md §3) used by both OpenOrFocusTab (Explorer clicks) and OpenFileCommand (File > Open File), so the rule lives in exactly one place.</summary>
    private TabViewModel CreateTabForPath(string path) =>
        path.EndsWith(".llx", StringComparison.OrdinalIgnoreCase)
            ? _tabFactory.CreateCnlTab(path)
            : _tabFactory.CreatePlainTextTab(path);

    private bool TryFocusExisting(string path)
    {
        var existing = OpenTabs.FirstOrDefault(t => string.Equals(t.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return false;
        }

        ActiveTab = existing;
        return true;
    }

    private bool TryCreateTab(string path, out TabViewModel tab)
    {
        try
        {
            tab = CreateTabForPath(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            FileTree.Errors.Add(new UiError
            {
                Code = "ERR_FILE_READ",
                Message = $"Could not open \"{path}\": {ex.Message}",
                Severity = ErrorSeverity.Error,
                Category = ErrorCategory.State,
            });
            tab = null!;
            return false;
        }
    }

    private void AddNewTab(TabViewModel tab)
    {
        tab.CloseRequested = () => CloseTabCommand.Execute(tab);
        OpenTabs.Add(tab);
        ActiveTab = tab;
        TabOpened?.Invoke(tab);
    }

    [RelayCommand]
    private void NewLlxFile() => AddNewTab(_tabFactory.CreateUntitledCnlTab(NextUntitledHeader()));

    [RelayCommand]
    private void NewTxtFile() => AddNewTab(_tabFactory.CreateUntitledPlainTextTab(NextUntitledHeader()));

    private string NextUntitledHeader() => $"Untitled-{++_untitledCounter}";

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var path = await _filePicker.PickAnyFileAsync();
        if (path is null || TryFocusExisting(path))
        {
            return;
        }

        if (TryCreateTab(path, out var tab))
        {
            AddNewTab(tab);
        }
    }

    [RelayCommand]
    private async Task CloseTabAsync(TabViewModel tab)
    {
        if (tab.IsDirty)
        {
            var discard = await _modal.ShowUnsavedChangesConfirmationAsync();
            if (!discard)
            {
                return;
            }
        }

        var wasActive = ActiveTab == tab;
        OpenTabs.Remove(tab);
        tab.Dispose();

        if (wasActive)
        {
            ActiveTab = OpenTabs.Count > 0 ? OpenTabs[^1] : null;
        }
    }

    [RelayCommand]
    private Task CloseActiveTabAsync() => ActiveTab is { } tab ? CloseTabCommand.ExecuteAsync(tab) : Task.CompletedTask;

    [RelayCommand]
    private void NextTab() => CycleTab(1);

    [RelayCommand]
    private void PreviousTab() => CycleTab(-1);

    private void CycleTab(int direction)
    {
        if (OpenTabs.Count == 0)
        {
            return;
        }

        var currentIndex = ActiveTab is null ? -1 : OpenTabs.IndexOf(ActiveTab);
        var nextIndex = ((currentIndex + direction) % OpenTabs.Count + OpenTabs.Count) % OpenTabs.Count;
        ActiveTab = OpenTabs[nextIndex];
    }

    /// <summary>ui-routing-navigation.md §3/§7: blocked while any tab's execution is in flight app-wide.</summary>
    [RelayCommand(CanExecute = nameof(CanOpenSettings))]
    private void OpenSettings() => IsSettingsOpen = true;

    private bool CanOpenSettings() => !_executionLock.IsAnyExecutionRunning;

    [RelayCommand]
    private void CloseSettings() => IsSettingsOpen = false;

    /// <summary>ui-routing-navigation.md §7.1: unlike Settings, never gated by execution state - About has no backend side effects.</summary>
    [RelayCommand]
    private void OpenAbout() => IsAboutOpen = true;

    [RelayCommand]
    private void CloseAbout() => IsAboutOpen = false;

    private void NotifyExecutionGatedCommandsCanExecuteChanged() => OpenSettingsCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => ActiveTab is { } tab ? SaveTabAsync(tab, forcePrompt: false) : Task.CompletedTask;

    private bool CanSave() => ActiveTab is not null;

    [RelayCommand(CanExecute = nameof(CanSaveAs))]
    private Task SaveAsAsync() => ActiveTab is { } tab ? SaveTabAsync(tab, forcePrompt: true) : Task.CompletedTask;

    private bool CanSaveAs() => ActiveTab is not null;

    [RelayCommand(CanExecute = nameof(CanSaveAll))]
    private async Task SaveAllAsync()
    {
        foreach (var tab in OpenTabs.Where(t => t.IsDirty).ToList())
        {
            await SaveTabAsync(tab, forcePrompt: false);
        }
    }

    private bool CanSaveAll() => OpenTabs.Any(t => t.IsDirty);

    /// <summary>
    /// Save on an untitled tab (or any tab when forcePrompt is true, i.e.
    /// Save As) prompts for a location; Save on a tab with an existing path
    /// writes directly. Returns false if the user cancelled the prompt or
    /// the write failed - callers (notably SaveAllAsync) use this to skip
    /// past a cancelled/failed tab rather than aborting (ui-viewmodels.md §3).
    /// </summary>
    private async Task<bool> SaveTabAsync(TabViewModel tab, bool forcePrompt)
    {
        var path = tab.FilePath;
        if (path is null || forcePrompt)
        {
            var suggestedName = path is not null ? Path.GetFileNameWithoutExtension(path) : tab.Header;
            var defaultExtension = tab is CnlTabViewModel ? "llx" : null;
            var picked = await _filePicker.PickSaveFileAsync(suggestedName, defaultExtension);
            if (picked is null)
            {
                return false;
            }

            path = picked;
        }

        try
        {
            await File.WriteAllTextAsync(path, GetTabContent(tab));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            tab.ErrorBanner.Show(new UiError
            {
                Code = "ERR_FILE_WRITE",
                Message = $"Could not save \"{path}\": {ex.Message}",
                Severity = ErrorSeverity.Error,
                Category = ErrorCategory.State,
            });
            return false;
        }

        tab.AssignFilePath(path);
        tab.IsDirty = false;
        return true;
    }

    private static string GetTabContent(TabViewModel tab) => tab switch
    {
        CnlTabViewModel cnl => cnl.Editor.Text,
        PlainTextTabViewModel txt => txt.Editor.Text,
        _ => throw new InvalidOperationException($"Unknown tab type {tab.GetType()}"),
    };

    private void OnOpenTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (TabViewModel tab in e.OldItems)
            {
                tab.PropertyChanged -= OnAnyTabDirtyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (TabViewModel tab in e.NewItems)
            {
                tab.PropertyChanged += OnAnyTabDirtyChanged;
            }
        }

        SaveAllCommand.NotifyCanExecuteChanged();
    }

    private void OnAnyTabDirtyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TabViewModel.IsDirty))
        {
            SaveAllCommand.NotifyCanExecuteChanged();
        }
    }
}
