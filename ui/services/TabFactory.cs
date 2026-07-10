using LimelightX.UI.Intellisense;
using LimelightX.UI.ViewModels.Tabs;

namespace LimelightX.UI.Services;

/// <summary>Concrete ITabFactory. Constructed once in the composition root, closing over the app-wide pipeline/event-stream/execution-lock/intellisense singletons.</summary>
public sealed class TabFactory(
    IPipelineService pipelineService,
    IEventStreamService eventStream,
    IExecutionLockService executionLock,
    ICompletionService completionService,
    IDiagnosticService diagnosticService,
    IHoverService hoverService,
    IFoldingService foldingService,
    IStructuralSelectionService structuralSelectionService) : ITabFactory
{
    public CnlTabViewModel CreateCnlTab(string filePath) =>
        new(filePath, File.ReadAllText(filePath), pipelineService, eventStream, executionLock, completionService, diagnosticService, hoverService, foldingService, structuralSelectionService);

    public PlainTextTabViewModel CreatePlainTextTab(string filePath) =>
        new(filePath, File.ReadAllText(filePath));

    public CnlTabViewModel CreateUntitledCnlTab(string header) =>
        new(header, pipelineService, eventStream, executionLock, completionService, diagnosticService, hoverService, foldingService, structuralSelectionService);

    public PlainTextTabViewModel CreateUntitledPlainTextTab(string header) =>
        new(header);
}
