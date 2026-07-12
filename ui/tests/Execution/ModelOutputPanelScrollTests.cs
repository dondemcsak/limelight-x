using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LimelightX.UI.Components;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Inspectors;
using Xunit;

namespace LimelightX.UI.Tests.Execution;

/// <summary>
/// Headless render tests for ModelOutputPanel's scroll-to-top-of-newest-entry
/// behavior (ui-components.md §5.6, bdd-ui-interactions.md §4.13) and the
/// layout-settle regression fix (bdd-ui-interactions.md §4.18), pinned to
/// the summarize-for-slack.llx repro (examples/summarize-for-slack.llx).
/// </summary>
public class ModelOutputPanelScrollTests
{
    private static ModelOutputBlock MakeOutput(int operationIndex, string text) => new()
    {
        OperationIndex = operationIndex,
        RawText = text,
        ContentType = LimelightX.UI.Services.Dto.ResultContentType.Markdown,
        Parsed = new ParsedContent(),
        Metadata = new ModelOutputMetadata { TokenUsage = text.Length / 4, LatencyMs = 100 },
    };

    [AvaloniaFact]
    public void ModelOutputPanel_AppendingEntries_ScrollsNewestEntryToTopOfPanel()
    {
        var viewModel = new ModelOutputViewModel { IsCollapsed = false, Height = 100 };
        var view = new ModelOutputPanel { DataContext = viewModel };
        var window = new Window { Content = view, Width = 400, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var longText = string.Concat(Enumerable.Repeat("Some fairly long model output text that wraps across several lines. ", 8));
        for (var i = 0; i < 3; i++)
        {
            viewModel.Outputs.Add(MakeOutput(i, longText));
            Dispatcher.UIThread.RunJobs();
        }

        var panel = view.FindControl<CollapsiblePanel>("Panel")!;
        var list = view.FindControl<ItemsControl>("OutputsList")!;
        var newest = (Control)list.ContainerFromIndex(2)!;

        var topInViewport = panel.TranslateToViewport(newest);

        Assert.True(Math.Abs(topInViewport.Y) < 1.0, $"Expected newest entry's top near 0, was {topInViewport.Y}");
    }

    [AvaloniaFact]
    public void ModelOutputPanel_RewriteEntry_ExtentReflectsFinalLayoutAndLastLineReachable()
    {
        // Mirrors examples/summarize-for-slack.llx: Load -> Extract (0) -> Summarize (1) -> Rewrite (2).
        var viewModel = new ModelOutputViewModel { IsCollapsed = false, Height = 150 };
        var view = new ModelOutputPanel { DataContext = viewModel };
        var window = new Window { Content = view, Width = 320, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        viewModel.Outputs.Add(MakeOutput(0, "- Ship the release notes\n- Follow up with design"));
        Dispatcher.UIThread.RunJobs();
        viewModel.Outputs.Add(MakeOutput(1, "The team needs to ship release notes and follow up with design."));
        Dispatcher.UIThread.RunJobs();

        var rewriteText = "Hey team! Quick update in a friendly, conversational tone suitable for a Slack update: " +
            string.Concat(Enumerable.Repeat("here is a long multi-sentence rewritten summary that must wrap across many lines so the card's height changes across layout passes. ", 12));
        viewModel.Outputs.Add(MakeOutput(2, rewriteText));
        Dispatcher.UIThread.RunJobs();

        var panel = view.FindControl<CollapsiblePanel>("Panel")!;
        var list = view.FindControl<ItemsControl>("OutputsList")!;
        var rewriteContainer = (Control)list.ContainerFromIndex(2)!;

        var containerBottomInContent = panel.TranslateToViewport(rewriteContainer).Y + panel.ContentOffset.Y + rewriteContainer.Bounds.Height;
        Assert.True(containerBottomInContent <= panel.ContentExtent.Height + 1.0,
            $"Rewrite card bottom ({containerBottomInContent}) exceeds scrollable extent ({panel.ContentExtent.Height}) - trailing content would be unreachable.");

        panel.ContentOffset = new Vector(0, Math.Max(0, panel.ContentExtent.Height - panel.ContentViewport.Height));
        Dispatcher.UIThread.RunJobs();

        var bottomInViewportAfterScroll = panel.TranslateToViewport(rewriteContainer).Y + rewriteContainer.Bounds.Height;
        Assert.True(bottomInViewportAfterScroll <= panel.ContentViewport.Height + 1.0,
            $"Rewrite card's last line (bottom at {bottomInViewportAfterScroll}) is not reachable within the viewport ({panel.ContentViewport.Height}).");
    }
}
