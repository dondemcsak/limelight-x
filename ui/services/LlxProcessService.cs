using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace LimelightX.UI.Services;

/// <summary>
/// Concrete ILlxProcessService. Detects successful bind by watching stdout
/// for the confirmed "Listening on http://" line (api.md §8); detects
/// failure by process exit + stderr content (src/cli/main.rs).
///
/// Shutdown is a direct Process.Kill(entireProcessTree: true), not a
/// graceful Ctrl+C signal. An earlier version tried the documented
/// FreeConsole/AttachConsole/GenerateConsoleCtrlEvent workaround (the plan's
/// flagged ambiguity #7, since .NET has no first-class API for sending
/// Ctrl+C to a child console process) - empirically, that crashed the
/// *calling* process too: GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0)
/// broadcasts to the entire console process group, not just the target, and
/// confirmed via a live test run that killed the xUnit test host itself
/// (exit code -1073741510 / STATUS_CONTROL_C_EXIT). A direct Kill sacrifices
/// "finish in-flight requests" graceful shutdown, but is safe.
/// </summary>
public sealed class LlxProcessService(string? executablePathOverride = null) : ILlxProcessService
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(5);

    private readonly string _executablePath = executablePathOverride ?? Path.Combine(AppContext.BaseDirectory, "llx.exe");

    private Process? _process;

    public bool IsRunning => _process is { HasExited: false };

    public async Task<ProcessStartOutcome> StartAsync(int port, string apiKey)
    {
        if (IsRunning)
        {
            await StopAsync().ConfigureAwait(false);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _executablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("serve");
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(port.ToString());
        startInfo.Environment["ANTHROPIC_API_KEY"] = apiKey;

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var readyOrExited = new TaskCompletionSource<bool>();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data?.Contains("Listening on http://", StringComparison.Ordinal) == true)
            {
                readyOrExited.TrySetResult(true);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };
        process.Exited += (_, _) => readyOrExited.TrySetResult(false);

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return new ProcessStartOutcome(false, $"Could not start llx.exe: {ex.Message}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var timeoutTask = Task.Delay(StartupTimeout);
        var completed = await Task.WhenAny(readyOrExited.Task, timeoutTask).ConfigureAwait(false);

        if (completed == timeoutTask || !await readyOrExited.Task.ConfigureAwait(false))
        {
            var message = stderr.Length > 0
                ? stderr.ToString().Trim()
                : "llx serve did not report a successful startup within the expected time.";

            KillIfRunning(process);
            return new ProcessStartOutcome(false, message);
        }

        _process = process;
        return new ProcessStartOutcome(true, null);
    }

    public Task StopAsync()
    {
        var process = _process;
        _process = null;

        if (process is not null)
        {
            KillIfRunning(process);
        }

        return Task.CompletedTask;
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Already exited between the check and the call - fine.
        }
    }
}
