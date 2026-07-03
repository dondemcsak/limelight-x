using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.Services;

/// <summary>
/// In-memory-only error log (ui-error-handling.md §11: "no file logging, no
/// developer console panel, no persistence across sessions"). No component
/// or page anywhere in the spec's catalog exposes a viewer for this - it
/// exists purely as an internal diagnostic aid.
/// </summary>
public interface ILogService
{
    IReadOnlyList<UiError> Entries { get; }

    void Log(UiError error);
}
