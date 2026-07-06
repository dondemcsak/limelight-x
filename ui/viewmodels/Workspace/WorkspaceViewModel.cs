using System.Collections.ObjectModel;
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
    }

    [RelayCommand]
    private void SelectTab(TabViewModel tab) => ActiveTab = tab;

    /// <summary>Stable binding target for global Run/Explain keyboard shortcuts (ui-accessibility.md §9) across tab switches - null when the active tab isn't a .llx tab, or there is none.</summary>
    public EditorViewModel? ActiveCnlEditor => (ActiveTab as CnlTabViewModel)?.Editor;

    [ObservableProperty]
    private bool _isSettingsOpen;

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

        var existing = OpenTabs.FirstOrDefault(t => string.Equals(t.FilePath, node.FullPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            ActiveTab = existing;
            return;
        }

        TabViewModel tab;
        try
        {
            tab = node.FullPath.EndsWith(".llx", StringComparison.OrdinalIgnoreCase)
                ? _tabFactory.CreateCnlTab(node.FullPath)
                : _tabFactory.CreatePlainTextTab(node.FullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            FileTree.Errors.Add(new UiError
            {
                Code = "ERR_FILE_READ",
                Message = $"Could not open \"{node.FullPath}\": {ex.Message}",
                Severity = ErrorSeverity.Error,
                Category = ErrorCategory.State,
            });
            return;
        }

        tab.CloseRequested = () => CloseTabCommand.Execute(tab);
        OpenTabs.Add(tab);
        ActiveTab = tab;
        TabOpened?.Invoke(tab);
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

    private void NotifyExecutionGatedCommandsCanExecuteChanged() => OpenSettingsCommand.NotifyCanExecuteChanged();
}
