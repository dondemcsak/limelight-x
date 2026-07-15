using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LimelightX.UI.Services;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.ViewModels;

/// <summary>
/// Holds and validates the editable deployment configuration
/// (ui-viewmodels.md §3.3). Bind host is fixed at 127.0.0.1 and never
/// editable - only Port is. Applies changes by stopping and relaunching
/// llx.exe serve via the shared LlxProcessService instance (same one
/// App.axaml.cs's startup sequence uses - no PID file, no IPC).
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigService _configService;
    private readonly ICredentialService _credentialService;
    private readonly ILlxProcessService _llxProcessService;

    private AppConfig _lastSaved = new();
    private string _lastSavedApiKey = string.Empty;

    public SettingsViewModel(IConfigService configService, ICredentialService credentialService, ILlxProcessService llxProcessService)
    {
        _configService = configService;
        _credentialService = credentialService;
        _llxProcessService = llxProcessService;

        LoadFromDisk();
    }

    [ObservableProperty]
    private int _port = 4747;

    [ObservableProperty]
    private string _logPath = string.Empty;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private EnvironmentProfile _environmentProfile = EnvironmentProfile.Dev;

    public IReadOnlyList<EnvironmentProfile> ProfileOptions { get; } = Enum.GetValues<EnvironmentProfile>();

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isApplying;

    [ObservableProperty]
    private bool _isApiKeyVisible;

    /// <summary>Client-side field validation errors (Validate(), below) - rendered inline via ValidationOverlay, distinct from ErrorBanner's relaunch-failure banner.</summary>
    public ObservableCollection<UiError> Errors { get; } = [];

    /// <summary>
    /// Relaunch-failure banner (ui-error-handling.md §7.5): on a failed
    /// relaunch the modal stays open and shows this instead of closing.
    /// SettingsViewModel is a single composition-root instance, not per-tab
    /// (ui-viewmodels.md §9).
    /// </summary>
    public ErrorBannerViewModel ErrorBanner { get; } = new();

    /// <summary>Set by the composition root to WorkspaceViewModel.CloseSettingsCommand - invoked on successful Save or a resolved Cancel.</summary>
    public Action? CloseRequested { get; set; }

    /// <summary>
    /// Set by the composition root to show the unsaved-changes Stay/Discard
    /// confirmation (same dialog as tab-close, ui-routing-navigation.md §3)
    /// when Cancel is clicked while IsDirty. Returns true if the user chose
    /// Discard.
    /// </summary>
    public Func<Task<bool>>? ConfirmDiscardChangesAsync { get; set; }

    /// <summary>
    /// Raised with the new port on a successful relaunch, so the composition
    /// root can repoint PipelineService and reconnect EventStreamService
    /// before the user can trigger another execution.
    /// </summary>
    public event Action<int>? RelaunchSucceeded;

    partial void OnPortChanged(int value) => IsDirty = true;

    partial void OnLogPathChanged(string value) => IsDirty = true;

    partial void OnApiKeyChanged(string value) => IsDirty = true;

    partial void OnEnvironmentProfileChanged(EnvironmentProfile value) => IsDirty = true;

    [RelayCommand]
    private void ToggleApiKeyVisibility() => IsApiKeyVisible = !IsApiKeyVisible;

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (!Validate())
        {
            return;
        }

        IsApplying = true;
        try
        {
            // Preserve LastOpenedFolder/RecentFolders (fields this ViewModel
            // never edits) via `with`, rather than resetting them to defaults -
            // note this can still lose a folder opened elsewhere in the same
            // session after this ViewModel's _lastSaved was last refreshed;
            // an acceptable narrow edge case for a persistence nicety.
            var config = _lastSaved with { Port = Port, LogPath = LogPath, EnvironmentProfile = EnvironmentProfile };

            _configService.Save(config);
            _credentialService.WriteApiKey(ApiKey);

            if (_llxProcessService.IsRunning)
            {
                await _llxProcessService.StopAsync();
            }

            var outcome = await _llxProcessService.StartAsync(Port, ApiKey);
            if (!outcome.Success)
            {
                // ui-error-handling.md §7.5: keep the modal open, show the
                // failure inline, and leave the previous backend connection
                // (if any) running until a restart succeeds.
                ErrorBanner.Show(new ApiError
                {
                    Code = "ERR_RELAUNCH_FAILED",
                    Message = outcome.ErrorMessage ?? "Failed to start llx serve with the new settings.",
                    Severity = ErrorSeverity.Fatal,
                    Category = ErrorCategory.Api,
                });
                return;
            }

            ErrorBanner.Clear();
            RelaunchSucceeded?.Invoke(Port);

            _lastSaved = config;
            _lastSavedApiKey = ApiKey;
            IsDirty = false;

            CloseRequested?.Invoke();
        }
        finally
        {
            IsApplying = false;
        }
    }

    [RelayCommand]
    private void RevertToLastSaved()
    {
        Port = _lastSaved.Port;
        LogPath = _lastSaved.LogPath;
        EnvironmentProfile = _lastSaved.EnvironmentProfile;
        ApiKey = _lastSavedApiKey;
        IsDirty = false;
    }

    [RelayCommand]
    private async Task CancelSettingsAsync()
    {
        if (IsDirty)
        {
            var discard = ConfirmDiscardChangesAsync is not null && await ConfirmDiscardChangesAsync();
            if (!discard)
            {
                return;
            }

            RevertToLastSavedCommand.Execute(null);
        }

        CloseRequested?.Invoke();
    }

    private void LoadFromDisk()
    {
        var config = _configService.Load() ?? new AppConfig();
        _lastSaved = config;
        _lastSavedApiKey = _credentialService.ReadApiKey() ?? string.Empty;

        Port = config.Port;
        LogPath = config.LogPath;
        EnvironmentProfile = config.EnvironmentProfile;
        ApiKey = _lastSavedApiKey;
        IsDirty = false;
    }

    private bool Validate()
    {
        Errors.Clear();

        if (Port is < 1 or > 65535)
        {
            Errors.Add(new ValidationError
            {
                Code = "ERR_INVALID_PORT",
                Message = "Port must be between 1 and 65535.",
                Severity = ErrorSeverity.Error,
                Category = ErrorCategory.Validation,
            });
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            Errors.Add(new ValidationError
            {
                Code = "ERR_MISSING_API_KEY",
                Message = "API key is required.",
                Severity = ErrorSeverity.Error,
                Category = ErrorCategory.Validation,
            });
        }

        if (!string.IsNullOrWhiteSpace(LogPath) && !Path.IsPathRooted(LogPath))
        {
            Errors.Add(new ValidationError
            {
                Code = "ERR_INVALID_LOG_PATH",
                Message = "Log path must be a valid absolute path.",
                Severity = ErrorSeverity.Error,
                Category = ErrorCategory.Validation,
            });
        }

        return Errors.Count == 0;
    }
}
