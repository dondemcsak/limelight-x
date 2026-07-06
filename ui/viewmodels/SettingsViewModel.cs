using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LimelightX.UI.Routing;
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

    public ObservableCollection<UiError> Errors { get; } = [];

    /// <summary>Set by the composition root; invoked on successful save to return to the previous page (or Home on first-launch).</summary>
    public Func<Task>? NavigateBackRequested { get; set; }

    /// <summary>
    /// Set by the composition root: implements Guard 5's Stay/Discard
    /// confirmation (ui-routing-navigation.md §4) if IsDirty, then navigates
    /// back immediately either way once resolved.
    /// </summary>
    public Func<Task>? CancelRequested { get; set; }

    /// <summary>Raised with a Category:Api, Severity:Fatal message on relaunch failure (ui-error-handling.md §10).</summary>
    public event Action<string>? RelaunchFailed;

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
            var config = new AppConfig { Port = Port, LogPath = LogPath, EnvironmentProfile = EnvironmentProfile };

            _configService.Save(config);
            _credentialService.WriteApiKey(ApiKey);

            if (_llxProcessService.IsRunning)
            {
                await _llxProcessService.StopAsync();
            }

            var outcome = await _llxProcessService.StartAsync(Port, ApiKey);
            if (!outcome.Success)
            {
                RelaunchFailed?.Invoke(outcome.ErrorMessage ?? "Failed to start llx serve with the new settings.");
                return;
            }

            RelaunchSucceeded?.Invoke(Port);

            _lastSaved = config;
            _lastSavedApiKey = ApiKey;
            IsDirty = false;

            if (NavigateBackRequested is not null)
            {
                await NavigateBackRequested();
            }
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
    private Task CancelSettingsAsync() => CancelRequested?.Invoke() ?? Task.CompletedTask;

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
