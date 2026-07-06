using LimelightX.UI.ViewModels.Tabs;

namespace LimelightX.UI.Services;

/// <summary>Concrete ITabFactory. Constructed once in the composition root, closing over the app-wide pipeline/event-stream/execution-lock singletons.</summary>
public sealed class TabFactory(
    IPipelineService pipelineService,
    IEventStreamService eventStream,
    IExecutionLockService executionLock) : ITabFactory
{
    public CnlTabViewModel CreateCnlTab(string filePath) =>
        new(filePath, File.ReadAllText(filePath), pipelineService, eventStream, executionLock);

    public PlainTextTabViewModel CreatePlainTextTab(string filePath) =>
        new(filePath, File.ReadAllText(filePath));
}
