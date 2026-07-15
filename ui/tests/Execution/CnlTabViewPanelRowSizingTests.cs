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
/// Regression tests for the accordion Grid row under-allocating space for
/// an expanded panel: CollapsiblePanel's true rendered height is its header
/// button PLUS its PanelHeight-tall content, but CnlTabView's row was only
/// ever sized to PanelHeight, clipping the bottom of every expanded panel
/// (including its own scrollbar) by exactly the header's height - reported
/// by the user as "not scrolling to the end of the content" in Prompt,
/// Model Output, and Final Result (ui-components.md §5.1 Layout Rules).
/// </summary>
public class CnlTabViewPanelRowSizingTests
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

    [AvaloniaFact]
    public async Task ExpandedPanelRow_ReservesHeaderHeightOnTopOfContentHeight()
    {
        var eventStream = new FakeEventStreamService();
        var tab = MakeTab(eventStream, new FakePipelineService());
        var view = new CnlTabView { DataContext = tab };
        var window = new Window { Content = view, Width = 500, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        await tab.PipelineExecution.RunPipelineAsync("Load the article from \"a.txt\".");
        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-run"));
        Dispatcher.UIThread.RunJobs();

        var contentHeightBeforeExpand = tab.PipelineExecution.RawAstViewModel.Height;

        eventStream.Raise(FakeEventStreamService.MakeEvent("raw_ast_generated", "corr-run",
            new RawAstEventData { RawAst = new RawAstResponse { Root = MakeAstRoot(), RawText = string.Empty, Metadata = new AstMetadata() } }));
        Dispatcher.UIThread.RunJobs();

        var panelStack = view.FindControl<Grid>("PanelStack")!;
        var rawAstRow = panelStack.RowDefinitions[0];

        Assert.True(rawAstRow.Height.IsAbsolute);
        Assert.Equal(contentHeightBeforeExpand + CollapsiblePanel.HeaderHeight, rawAstRow.Height.Value, precision: 3);
    }

    [AvaloniaFact]
    public async Task ExpandedPanel_TotalRenderedHeightFitsWithinItsRow_NothingClipped()
    {
        var eventStream = new FakeEventStreamService();
        var tab = MakeTab(eventStream, new FakePipelineService());
        var view = new CnlTabView { DataContext = tab };
        var window = new Window { Content = view, Width = 500, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        await tab.PipelineExecution.RunPipelineAsync("Load the article from \"a.txt\".");
        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-run"));
        Dispatcher.UIThread.RunJobs();

        eventStream.Raise(FakeEventStreamService.MakeEvent("raw_ast_generated", "corr-run",
            new RawAstEventData { RawAst = new RawAstResponse { Root = MakeAstRoot(), RawText = string.Empty, Metadata = new AstMetadata() } }));
        Dispatcher.UIThread.RunJobs();

        var panelStack = view.FindControl<Grid>("PanelStack")!;
        var rawAstRow = panelStack.RowDefinitions[0];
        var rawAstPanelControl = (Control)panelStack.Children.Single(c => Grid.GetRow(c) == 0);

        Assert.True(rawAstPanelControl.Bounds.Height <= rawAstRow.Height.Value + 0.5,
            $"RawAstPanel's rendered height ({rawAstPanelControl.Bounds.Height}) exceeds its allocated row ({rawAstRow.Height.Value}) - its content (including its own scrollbar) would be clipped by the Grid cell.");
    }

    [AvaloniaFact]
    public async Task DraggingSplitter_WritesBackContentHeightExcludingHeaderHeight()
    {
        var eventStream = new FakeEventStreamService();
        var tab = MakeTab(eventStream, new FakePipelineService());
        var view = new CnlTabView { DataContext = tab };
        var window = new Window { Content = view, Width = 500, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        await tab.PipelineExecution.RunPipelineAsync("Load the article from \"a.txt\".");
        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-run"));
        Dispatcher.UIThread.RunJobs();

        eventStream.Raise(FakeEventStreamService.MakeEvent("raw_ast_generated", "corr-run",
            new RawAstEventData { RawAst = new RawAstResponse { Root = MakeAstRoot(), RawText = string.Empty, Metadata = new AstMetadata() } }));
        Dispatcher.UIThread.RunJobs();

        var panelStack = view.FindControl<Grid>("PanelStack")!;
        var rawAstRow = panelStack.RowDefinitions[0];

        rawAstRow.Height = new GridLength(300, GridUnitType.Pixel);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(300 - CollapsiblePanel.HeaderHeight, tab.PipelineExecution.RawAstViewModel.Height, precision: 3);
    }
}
