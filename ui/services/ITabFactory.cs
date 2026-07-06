using LimelightX.UI.ViewModels.Tabs;

namespace LimelightX.UI.Services;

/// <summary>
/// Creates tab ViewModels for newly opened files (ui-viewmodels.md §5).
/// Exists so WorkspaceViewModel never touches IPipelineService/
/// IEventStreamService/IExecutionLockService directly (ui-routing-navigation.md
/// §10's "Must Not" list) - those are closed over here instead.
/// </summary>
public interface ITabFactory
{
    /// <summary>Reads <paramref name="filePath"/> synchronously and builds a CnlTabViewModel from its contents. Throws IOException/UnauthorizedAccessException on read failure.</summary>
    CnlTabViewModel CreateCnlTab(string filePath);

    /// <summary>Reads <paramref name="filePath"/> synchronously and builds a PlainTextTabViewModel from its contents. Throws IOException/UnauthorizedAccessException on read failure.</summary>
    PlainTextTabViewModel CreatePlainTextTab(string filePath);
}
