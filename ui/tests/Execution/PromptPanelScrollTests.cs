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
/// Headless render tests for PromptPanel's scroll-to-top-of-newest-entry
/// behavior (ui-components.md §5.5, bdd-ui-interactions.md §4.12) and the
/// layout-settle regression fix (bdd-ui-interactions.md §4.17), pinned to
/// the summarize-for-slack.llx repro (examples/summarize-for-slack.llx).
/// </summary>
public class PromptPanelScrollTests
{
    private static PromptBlock MakePrompt(int operationIndex, string text) => new()
    {
        OperationIndex = operationIndex,
        PromptText = text,
        Metadata = new PromptBlockMetadata { Length = text.Length },
    };

    [AvaloniaFact]
    public void PromptPanel_AppendingEntries_ScrollsNewestEntryToTopOfPanel()
    {
        var viewModel = new PromptViewModel { IsCollapsed = false, Height = 100 };
        var view = new PromptPanel { DataContext = viewModel };
        var window = new Window { Content = view, Width = 400, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var longText = string.Concat(Enumerable.Repeat("Some fairly long prompt text that wraps across several lines. ", 8));
        for (var i = 0; i < 3; i++)
        {
            viewModel.Prompts.Add(MakePrompt(i, longText));
            Dispatcher.UIThread.RunJobs();
        }

        var panel = view.FindControl<CollapsiblePanel>("Panel")!;
        var list = view.FindControl<ItemsControl>("PromptsList")!;
        var newest = (Control)list.ContainerFromIndex(2)!;

        var topInViewport = panel.TranslateToViewport(newest);

        Assert.True(Math.Abs(topInViewport.Y) < 1.0, $"Expected newest entry's top near 0, was {topInViewport.Y}");
    }

    [AvaloniaFact]
    public void PromptPanel_RewriteEntry_ExtentReflectsFinalLayoutAndLastLineReachable()
    {
        // Mirrors examples/summarize-for-slack.llx: Load -> Extract (0) -> Summarize (1) -> Rewrite (2),
        // whose prompt embeds the custom instruction verbatim.
        var viewModel = new PromptViewModel { IsCollapsed = false, Height = 150 };
        var view = new PromptPanel { DataContext = viewModel };
        var window = new Window { Content = view, Width = 320, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        viewModel.Prompts.Add(MakePrompt(0, "Extract the action items from the transcript."));
        Dispatcher.UIThread.RunJobs();
        viewModel.Prompts.Add(MakePrompt(1, "Summarize the action items."));
        Dispatcher.UIThread.RunJobs();

        var rewriteText = "Rewrite the summary using the following instruction verbatim: " +
            "\"Rewrite in a friendly, conversational tone suitable for a Slack update.\" " +
            string.Concat(Enumerable.Repeat("Here is a long multi-sentence summary that must wrap across many lines so the card's height changes across layout passes. ", 12));
        viewModel.Prompts.Add(MakePrompt(2, rewriteText));
        Dispatcher.UIThread.RunJobs();

        var panel = view.FindControl<CollapsiblePanel>("Panel")!;
        var list = view.FindControl<ItemsControl>("PromptsList")!;
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
