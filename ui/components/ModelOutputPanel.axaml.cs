using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Threading;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Inspectors;

namespace LimelightX.UI.Components;

/// <summary>
/// Syntax-highlighted Markdown/JSON model output rendering (ui-components.md
/// §4.5). Trace-only. Renders from ModelOutputBlock.RawText, not
/// Parsed.Markdown (confirmed always null server-side today).
/// </summary>
public partial class ModelOutputPanel : UserControl
{
    private ModelOutputViewModel? _subscribedViewModel;

    public ModelOutputPanel()
    {
        InitializeComponent();

        OutputsList.ItemTemplate = new FuncDataTemplate<ModelOutputBlock>((block, _) =>
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
            content.Children.Add(ContentRenderer.Render(block.RawText, MapContentType(block.ContentType)));
            content.Children.Add(new TextBlock
            {
                Text = $"{block.Metadata.TokenUsage} tokens · {block.Metadata.LatencyMs} ms",
                Foreground = (IBrush)Application.Current!.FindResource("TextMutedBrush")!,
                FontFamily = (FontFamily)Application.Current!.FindResource("InterFontFamily")!,
                FontSize = 11,
            });

            card.Child = content;
            return card;
        });

        DataContextChanged += (_, _) => AttachViewModel();
    }

    private void AttachViewModel()
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.Outputs.CollectionChanged -= OnOutputsChanged;
        }

        if (DataContext is ModelOutputViewModel viewModel)
        {
            viewModel.Outputs.CollectionChanged += OnOutputsChanged;
            _subscribedViewModel = viewModel;
        }
    }

    /// <summary>
    /// Scrolls the newest entry to the top of the panel on every append,
    /// unconditionally - no scroll-position tracking (ui-components.md §5.6,
    /// bdd-ui-interactions.md §4.13). Re-scrolls on every LayoutUpdated for
    /// the newest entry's container, so the final firing - after wrapped
    /// Markdown's constrained-width arrange pass settles - wins, keeping the
    /// scrollable extent accurate and the entry's trailing content reachable
    /// (bdd-ui-interactions.md §4.18).
    /// </summary>
    private void OnOutputsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            Dispatcher.UIThread.Post(ScheduleScrollToNewest, DispatcherPriority.Loaded);
        }
    }

    private void ScheduleScrollToNewest()
    {
        if (OutputsList.ContainerFromIndex(OutputsList.ItemCount - 1) is not Control container)
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

    private static ViewModels.Inspectors.ResultContentType MapContentType(Services.Dto.ResultContentType wireType) => wireType switch
    {
        Services.Dto.ResultContentType.Markdown => ViewModels.Inspectors.ResultContentType.Markdown,
        Services.Dto.ResultContentType.Json => ViewModels.Inspectors.ResultContentType.Json,
        _ => ViewModels.Inspectors.ResultContentType.PlainText,
    };
}
