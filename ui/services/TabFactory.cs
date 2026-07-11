using LimelightX.UI.Intellisense;
using LimelightX.UI.ViewModels.Tabs;

namespace LimelightX.UI.Services;

/// <summary>
/// Concrete ITabFactory. Constructed once in the composition root, closing
/// over the app-wide pipeline/event-stream/execution-lock/intellisense
/// singletons. parserHostFactory defaults to the real, native-DLL-backed
/// ParserHost (production behavior); tests pass a fake here instead of
/// letting CnlTabViewModel construct one directly, so opening a .llx file in
/// a Workspace/Execution test never P/Invokes the ARM64-only DLL on CI
/// (CLAUDE.md §3.5).
/// </summary>
public sealed class TabFactory(
    IPipelineService pipelineService,
    IEventStreamService eventStream,
    IExecutionLockService executionLock,
    ICompletionService completionService,
    IDiagnosticService diagnosticService,
    IHoverService hoverService,
    IFoldingService foldingService,
    IStructuralSelectionService structuralSelectionService,
    IOutlineService outlineService,
    IAutoPairService autoPairService,
    INavigationService navigationService,
    Func<IParserHost>? parserHostFactory = null) : ITabFactory
{
    private readonly Func<IParserHost> _parserHostFactory = parserHostFactory ?? (() => new ParserHost());

    public CnlTabViewModel CreateCnlTab(string filePath) =>
        new(filePath, File.ReadAllText(filePath), pipelineService, eventStream, executionLock, completionService, diagnosticService, hoverService, foldingService, structuralSelectionService, outlineService, autoPairService, navigationService, _parserHostFactory);

    public PlainTextTabViewModel CreatePlainTextTab(string filePath) =>
        new(filePath, File.ReadAllText(filePath));

    public CnlTabViewModel CreateUntitledCnlTab(string header) =>
        new(header, pipelineService, eventStream, executionLock, completionService, diagnosticService, hoverService, foldingService, structuralSelectionService, outlineService, autoPairService, navigationService, _parserHostFactory);

    public PlainTextTabViewModel CreateUntitledPlainTextTab(string header) =>
        new(header);
}
