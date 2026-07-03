namespace LimelightX.UI.Services;

/// <summary>
/// Owns the single llx.exe serve child process (ui-deployment.md §7,
/// ui-viewmodels.md §3.3 "Process Ownership"). LimelightX.exe launches it at
/// startup and terminates it on exit; SettingsViewModel reuses this same
/// service to stop/relaunch on a new port.
/// </summary>
public interface ILlxProcessService
{
    bool IsRunning { get; }

    Task<ProcessStartOutcome> StartAsync(int port, string apiKey);

    Task StopAsync();
}
