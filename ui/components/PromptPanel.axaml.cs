using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Inspectors;

namespace LimelightX.UI.Components;

/// <summary>
/// Prompt blocks, syntax-highlighted Markdown, operation index labels
/// (ui-components.md §4.4). Trace-only. Uses a code-behind FuncDataTemplate
/// since rendering Markdown requires building controls imperatively
/// (MarkdownRenderer.Render), not a pure XAML DataTemplate.
/// </summary>
public partial class PromptPanel : UserControl
{
    private PromptViewModel? _subscribedViewModel;

    public PromptPanel()
    {
        InitializeComponent();

        PromptsList.ItemTemplate = new FuncDataTemplate<PromptBlock>((block, _) =>
        {
            var card = new Border
            {
                Classes = { "card" },
                Margin = new Thickness(0, 0, 0, 8),
                Padding = (Thickness)Application.Current!.FindResource("PaddingMediumThickness")!,
            };

            var content = new StackPanel { Spacing = 4 };
            content.Children.Add(new TextBlock
            {
                Text = $"operation {block.OperationIndex}",
                Foreground = (IBrush)Application.Current!.FindResource("TextMutedBrush")!,
                FontFamily = (FontFamily)Application.Current!.FindResource("JetBrainsMonoFontFamily")!,
                FontSize = 11,
            });
            content.Children.Add(MarkdownRenderer.Render(block.PromptText));

            card.Child = content;
            return card;
        });

        DataContextChanged += (_, _) => AttachViewModel();
    }

    private void AttachViewModel()
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.Prompts.CollectionChanged -= OnPromptsChanged;
        }

        if (DataContext is PromptViewModel viewModel)
        {
            viewModel.Prompts.CollectionChanged += OnPromptsChanged;
            _subscribedViewModel = viewModel;
        }
    }

    /// <summary>
    /// Scrolls the newest entry to the top of the panel on every append,
    /// unconditionally - no scroll-position tracking (ui-components.md §5.5,
    /// bdd-ui-interactions.md §4.12). Re-scrolls on every LayoutUpdated for
    /// the newest entry's container, so the final firing - after wrapped
    /// Markdown's constrained-width arrange pass settles - wins, keeping the
    /// scrollable extent accurate and the entry's trailing content reachable
    /// (bdd-ui-interactions.md §4.17).
    /// </summary>
    private void OnPromptsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            Dispatcher.UIThread.Post(ScheduleScrollToNewest, DispatcherPriority.Loaded);
        }
    }

    private void ScheduleScrollToNewest()
    {
        if (PromptsList.ContainerFromIndex(PromptsList.ItemCount - 1) is not Control container)
        {
            Dispatcher.UIThread.Post(ScheduleScrollToNewest, DispatcherPriority.Loaded);
            return;
        }

        Panel.ScrollContentToTopOf(container);

        // Wrapped Markdown can still change height across further layout
        // passes - keep re-scrolling until the scrollable extent stops
        // changing (settled), unsubscribing from inside the handler itself
        // (not via a second posted callback, so there's no cross-priority
        // race over which runs first). RemainingSafetyFirings is only a
        // backstop against a control that never stabilizes.
        var lastExtentHeight = Panel.ContentExtent.Height;
        var remainingSafetyFirings = 20;
        void OnLayoutUpdated(object? s, EventArgs args)
        {
            Panel.ScrollContentToTopOf(container);
            var extentHeight = Panel.ContentExtent.Height;
            var settled = Math.Abs(extentHeight - lastExtentHeight) < 0.5;
            lastExtentHeight = extentHeight;

            if (settled || --remainingSafetyFirings <= 0)
            {
                container.LayoutUpdated -= OnLayoutUpdated;
            }
        }

        container.LayoutUpdated += OnLayoutUpdated;
    }
}
