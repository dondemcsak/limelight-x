using LimelightX.UI.ViewModels.Errors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace LimelightX.UI.Services;

/// <summary>
/// Builds the persistent diagnostic log's ILoggerFactory (ui-deployment.md
/// §4.3, ui-error-handling.md §2.5): Microsoft.Extensions.Logging as the
/// logging API, Serilog's file sink as the provider, writing plain-text
/// lines to `Limelight-x-log.txt` in the resolved LogPath directory
/// (empty/unset LogPath falls back to config.json's own directory).
///
/// Returns ILoggerFactory (not just ILogger) so callers can Dispose() it to
/// release the underlying file handle - needed when SettingsViewModel.LogPath
/// changes and logging must redirect immediately (ui-viewmodels.md §7).
/// </summary>
public static class AppLogging
{
    private const string LogFileName = "Limelight-x-log.txt";

    public static ILoggerFactory CreateLoggerFactory(string? logPath, string configFilePath)
    {
        try
        {
            var directory = string.IsNullOrWhiteSpace(logPath)
                ? Path.GetDirectoryName(configFilePath) is { Length: > 0 } configDirectory
                    ? configDirectory
                    : AppContext.BaseDirectory
                : logPath;

            Directory.CreateDirectory(directory);

            // The [{LevelName}] bracket comes from the message template in
            // LogUiError below, not Serilog's own {Level} token: Serilog's
            // provider bridge maps MEL's LogLevel.Critical to Serilog's own
            // LogEventLevel.Fatal, which would render as "Fatal" here and
            // break the documented Fatal->Critical severity mapping
            // (ui-deployment.md §4.3) if {Level} were used instead.
            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.File(
                    Path.Combine(directory, LogFileName),
                    outputTemplate: "[{Timestamp:u}] {Message:lj}{NewLine}")
                .CreateLogger();

            var factory = new LoggerFactory();
            factory.AddSerilog(serilogLogger, dispose: true);
            return factory;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Failure safety (ui-error-handling.md §2.5): a logging setup
            // failure must never throw, crash the app, or block anything else.
            return NullLoggerFactory.Instance;
        }
    }

    /// <summary>ui-error-handling.md §2.5: every UiError surfaced to the user is also logged.</summary>
    public static void LogUiError(ILogger logger, UiError error)
    {
        var level = MapLogLevel(error.Severity);
        var message = error.Location is { } location
            ? $"{error.Message} (line {location.Line}, column {location.Column})"
            : error.Message;

        // {LevelName} is the literal MEL LogLevel name (e.g. "Critical"), not
        // Serilog's own vocabulary - see the comment on the output template
        // above. Both enums are passed via ToString(), not the enum values
        // themselves, since Serilog quotes captured enum values (e.g.
        // "Critical") by default.
        logger.Log(level, "[{LevelName}] {Code}: {Message} (Category={Category})", level.ToString(), error.Code, message, error.Category.ToString());
    }

    /// <summary>ui-deployment.md §4.3 severity mapping.</summary>
    public static LogLevel MapLogLevel(ErrorSeverity severity) => severity switch
    {
        ErrorSeverity.Info => LogLevel.Information,
        ErrorSeverity.Warning => LogLevel.Warning,
        ErrorSeverity.Error => LogLevel.Error,
        ErrorSeverity.Fatal => LogLevel.Critical,
        _ => LogLevel.Information,
    };
}
