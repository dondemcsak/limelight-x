using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LimelightX.UI.Components;
using LimelightX.UI.Services;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.Tests.TestDoubles;
using LimelightX.UI.ViewModels.Tabs;
using Xunit;

namespace LimelightX.UI.Tests.Execution;

/// <summary>
/// Headless render tests for CnlTabView's outer-scroll-to-Prompt-panel
/// behavior on the first prompt_generated event of a run
/// (ui-architecture.md §7 "Outer Scroll Behavior", bdd-ui-interactions.md §4.16).
/// </summary>
public class CnlTabViewOuterScrollTests
{
    private sealed class FakePipelineService : IPipelineService
    {
        public PipelineStartResult TraceResultToReturn { get; set; } = new() { Accepted = true, CorrelationId = "corr-run" };

        public Task<PipelineStartResult> ExplainAsync(string source) => throw new NotImplementedException();

        public Task<PipelineStartResult> RunAsync(string source) => throw new NotImplementedException();

        public Task<PipelineStartResult> TraceAsync(string source) => Task.FromResult(TraceResultToReturn);
    }

    private static AstNode MakeAstRoot() => new()
    {
        Type = "Program",
        Value = string.Empty,
        Span = new Span(),
        Metadata = new AstNodeMetadata(),
        Children = [],
    };

    private static CnlTabViewModel MakeTab(FakeEventStreamService eventStream, IPipelineService pipeline) => new(
        "untitled",
        pipeline,
        eventStream,
        new ExecutionLockService(),
        new FakeCompletionService(),
        new FakeDiagnosticService(),
        new FakeHoverService(),
        new FakeFoldingService(),
        new FakeStructuralSelectionService(),
        new FakeOutlineService(),
        new FakeAutoPairService(),
        new FakeNavigationService(),
        () => new FakeParserHost());

    /// <summary>Expands Raw AST, Normalized AST, and IR (each to their default 220px height), pushing PromptPanel below the fold in a modestly sized window.</summary>
    private static void ExpandPrecedingPanels(FakeEventStreamService eventStream)
    {
        eventStream.Raise(FakeEventStreamService.MakeEvent("raw_ast_generated", "corr-run",
            new RawAstEventData { RawAst = new RawAstResponse { Root = MakeAstRoot(), RawText = string.Empty, Metadata = new AstMetadata() } }));
        Dispatcher.UIThread.RunJobs();

        eventStream.Raise(FakeEventStreamService.MakeEvent("normalized_ast_generated", "corr-run",
            new NormalizedAstEventData { NormalizedAst = new NormalizedAstResponse { Root = MakeAstRoot(), RawText = string.Empty, Metadata = new NormalizedAstMetadata() } }));
        Dispatcher.UIThread.RunJobs();

        eventStream.Raise(FakeEventStreamService.MakeEvent("ir_generated", "corr-run",
            new IrEventData { Ir = new IrResponse { RawText = string.Empty, Metadata = new IrMetadata() } }));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task CnlTabView_FirstPromptGenerated_ScrollsOuterViewerToPromptPanelTop()
    {
        var eventStream = new FakeEventStreamService();
        var tab = MakeTab(eventStream, new FakePipelineService());

        var view = new CnlTabView { DataContext = tab };
        var window = new Window { Content = view, Width = 500, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        await tab.PipelineExecution.RunPipelineAsync("Load the article from \"a.txt\".\nSummarize it.");
        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-run"));
        Dispatcher.UIThread.RunJobs();

        ExpandPrecedingPanels(eventStream);

        var panelStackScrollViewer = view.FindControl<ScrollViewer>("PanelStackScrollViewer")!;
        var promptPanelView = view.FindControl<PromptPanel>("PromptPanelView")!;

        eventStream.Raise(FakeEventStreamService.MakeEvent("prompt_generated", "corr-run",
            new PromptEventData { Prompt = new PromptBlock { OperationIndex = 0, PromptText = "Summarize this", Metadata = new PromptBlockMetadata() } }));
        Dispatcher.UIThread.RunJobs();

        var topInViewport = promptPanelView.TranslatePoint(new Point(0, 0), panelStackScrollViewer) ?? default;

        Assert.True(topInViewport.Y >= -1.0 && topInViewport.Y <= 1.0,
            $"Expected PromptPanel's top aligned with the outer viewport's top after the first prompt, was {topInViewport.Y}");
    }

    [AvaloniaFact]
    public async Task CnlTabView_SecondPromptGenerated_DoesNotReTriggerOuterScroll()
    {
        var eventStream = new FakeEventStreamService();
        var tab = MakeTab(eventStream, new FakePipelineService());

        var view = new CnlTabView { DataContext = tab };
        var window = new Window { Content = view, Width = 500, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        await tab.PipelineExecution.RunPipelineAsync("Load the article from \"a.txt\".\nSummarize it.\nTranslate it.");
        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-run"));
        Dispatcher.UIThread.RunJobs();

        ExpandPrecedingPanels(eventStream);

        var panelStackScrollViewer = view.FindControl<ScrollViewer>("PanelStackScrollViewer")!;

        eventStream.Raise(FakeEventStreamService.MakeEvent("prompt_generated", "corr-run",
            new PromptEventData { Prompt = new PromptBlock { OperationIndex = 0, PromptText = "Summarize this", Metadata = new PromptBlockMetadata() } }));
        Dispatcher.UIThread.RunJobs();

        // Perturb the offset after the first (auto) outer scroll so a spurious second scroll would be detectable.
        panelStackScrollViewer.Offset = new Vector(0, panelStackScrollViewer.Offset.Y + 37);
        Dispatcher.UIThread.RunJobs();
        var offsetAfterPerturb = panelStackScrollViewer.Offset;

        eventStream.Raise(FakeEventStreamService.MakeEvent("prompt_generated", "corr-run",
            new PromptEventData { Prompt = new PromptBlock { OperationIndex = 1, PromptText = "Translate this", Metadata = new PromptBlockMetadata() } }));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(offsetAfterPerturb.Y, panelStackScrollViewer.Offset.Y, precision: 3);
    }
}
