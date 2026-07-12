using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using LimelightX.UI.ViewModels.Inspectors;
using LimelightX.UI.ViewModels.Tabs;

namespace LimelightX.UI.Components;

/// <summary>
/// Syncs the editor/execution-panel split and each inspector panel's
/// accordion row against this tab's own ViewModel state
/// (ui-viewmodels.md §5.2, §11), since TabContentHost recreates this View
/// whenever the active tab changes and changes back - anything kept only in
/// SplitGrid/PanelStack's RowDefinitions would otherwise be lost on tab
/// switch.
/// </summary>
public partial class CnlTabView : UserControl
{
    private readonly List<Action> _unsubscribers = [];

    public CnlTabView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachViewModel();
    }

    private void AttachViewModel()
    {
        foreach (var unsubscribe in _unsubscribers)
        {
            unsubscribe();
        }

        _unsubscribers.Clear();

        if (DataContext is not CnlTabViewModel viewModel)
        {
            return;
        }

        BindEditorSplit(viewModel);
        BindPanelRow(PanelStack.RowDefinitions[0], viewModel.PipelineExecution.RawAstViewModel);
        BindPanelRow(PanelStack.RowDefinitions[2], viewModel.PipelineExecution.NormalizedAstViewModel);
        BindPanelRow(PanelStack.RowDefinitions[4], viewModel.PipelineExecution.IrViewModel);
        BindPanelRow(PanelStack.RowDefinitions[6], viewModel.PipelineExecution.PromptViewModel);
        BindPanelRow(PanelStack.RowDefinitions[8], viewModel.PipelineExecution.ModelOutputViewModel);
        BindPanelRow(PanelStack.RowDefinitions[10], viewModel.PipelineExecution.FinalResultViewModel);
        BindOuterPromptScroll(viewModel);
        BindUndoRedo(viewModel);
        BindEditorTextSync(viewModel);
    }

    /// <summary>
    /// Forwards EditorViewModel.UndoRequested/RedoRequested (raised by
    /// Ctrl+Z/Ctrl+Y via EditorViewModel.UndoCommand/RedoCommand) into this
    /// tab's own CnlEditor, which owns the actual AvaloniaEdit-backed undo
    /// history (ui-components.md §4.2). Per-tab by construction: each
    /// CnlTabView/CnlEditor pair is unique to its own CnlTabViewModel.
    /// </summary>
    private void BindUndoRedo(CnlTabViewModel viewModel)
    {
        void OnUndoRequested() => CnlEditorView.Undo();
        void OnRedoRequested() => CnlEditorView.Redo();

        viewModel.Editor.UndoRequested += OnUndoRequested;
        viewModel.Editor.RedoRequested += OnRedoRequested;
        _unsubscribers.Add(() => viewModel.Editor.UndoRequested -= OnUndoRequested);
        _unsubscribers.Add(() => viewModel.Editor.RedoRequested -= OnRedoRequested);
    }

    /// <summary>
    /// Explicit, supplementary sync from CnlEditorView.Text into
    /// viewModel.Editor.Text, on top of the compiled TwoWay Text="{Binding
    /// Editor.Text}" binding above (in the .axaml). That compiled binding
    /// reliably pushes insertion-driven changes (ordinary typing) back to
    /// the ViewModel, but does not reliably push deletion-driven changes
    /// (Backspace/Delete, and - critically for Undo/Redo, which are
    /// themselves document-removal-shaped operations - Ctrl+Z/Ctrl+Y) - a
    /// pre-existing gap in how Avalonia's compiled TwoWay binding reacts to
    /// SetCurrentValue for this control, unrelated to undo/redo specifically
    /// (confirmed by reproducing it with a plain Backspace keystroke).
    /// CnlEditorView.Text (the StyledProperty) itself reliably raises its
    /// own AvaloniaPropertyChanged for both insertions and deletions, so
    /// mirroring it here directly is a safe, always-correct guarantee -
    /// redundant with the compiled binding for insertions (CommunityToolkit's
    /// generated setter no-ops when the incoming value already matches), and
    /// the only thing that actually propagates deletions.
    /// </summary>
    private void BindEditorTextSync(CnlTabViewModel viewModel)
    {
        void OnCnlEditorTextChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == CnlEditor.TextProperty)
            {
                viewModel.Editor.Text = CnlEditorView.Text;
                viewModel.RecomputeIsDirty();
            }
        }

        CnlEditorView.PropertyChanged += OnCnlEditorTextChanged;
        _unsubscribers.Add(() => CnlEditorView.PropertyChanged -= OnCnlEditorTextChanged);
    }

    /// <summary>
    /// Scrolls PanelStackScrollViewer so PromptPanelView's top edge aligns
    /// with the outer viewport's top, exactly once per run, the moment the
    /// first prompt_generated event of that run arrives - if the panel
    /// isn't already fully visible (ui-architecture.md §7 "Outer Scroll
    /// Behavior", bdd-ui-interactions.md §4.16). Prompts.Count == 1 doubles
    /// as the "first prompt this run" latch - PromptViewModel.Reset() clears
    /// Prompts on every new run, so this re-arms automatically.
    /// </summary>
    private void BindOuterPromptScroll(CnlTabViewModel viewModel)
    {
        var prompts = viewModel.PipelineExecution.PromptViewModel.Prompts;

        void OnPromptsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && prompts.Count == 1)
            {
                Dispatcher.UIThread.Post(ScrollPromptPanelIntoView, DispatcherPriority.Loaded);
            }
        }

        prompts.CollectionChanged += OnPromptsChanged;
        _unsubscribers.Add(() => prompts.CollectionChanged -= OnPromptsChanged);
    }

    private void ScrollPromptPanelIntoView()
    {
        var topLeft = PromptPanelView.TranslatePoint(new Point(0, 0), PanelStackScrollViewer) ?? default;
        var fullyVisible = topLeft.Y >= 0
            && topLeft.Y + PromptPanelView.Bounds.Height <= PanelStackScrollViewer.Viewport.Height;

        if (!fullyVisible)
        {
            var offset = PanelStackScrollViewer.Offset;
            PanelStackScrollViewer.Offset = new Vector(offset.X, offset.Y + topLeft.Y);
        }
    }

    /// <summary>Two-way syncs SplitGrid's two star-sized rows against CnlTabViewModel.EditorPaneRatio.</summary>
    private void BindEditorSplit(CnlTabViewModel viewModel)
    {
        var editorRow = SplitGrid.RowDefinitions[0];
        var panelsRow = SplitGrid.RowDefinitions[2];

        editorRow.Height = new GridLength(viewModel.EditorPaneRatio, GridUnitType.Star);
        panelsRow.Height = new GridLength(1 - viewModel.EditorPaneRatio, GridUnitType.Star);

        void OnRowHeightChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != RowDefinition.HeightProperty)
            {
                return;
            }

            var total = editorRow.Height.Value + panelsRow.Height.Value;
            if (total > 0)
            {
                viewModel.EditorPaneRatio = editorRow.Height.Value / total;
            }
        }

        editorRow.PropertyChanged += OnRowHeightChanged;
        panelsRow.PropertyChanged += OnRowHeightChanged;
        _unsubscribers.Add(() => editorRow.PropertyChanged -= OnRowHeightChanged);
        _unsubscribers.Add(() => panelsRow.PropertyChanged -= OnRowHeightChanged);
    }

    /// <summary>
    /// Two-way syncs one accordion row against its panel's IsCollapsed/Height:
    /// collapsed -> Auto (header-only); expanded -> a pixel Height matching
    /// the ViewModel plus CollapsiblePanel.HeaderHeight, updated as the user
    /// drags this row's GridSplitter. panelViewModel.Height represents only
    /// the content area (CollapsiblePanel.PanelHeight's own documented
    /// meaning); the row must additionally reserve room for the header
    /// button rendered above that content, or the panel's true total height
    /// overflows the row and its bottom - including part of the content's
    /// own scrollbar - gets clipped by the Grid cell (ui-components.md §5.1
    /// Layout Rules).
    /// </summary>
    private void BindPanelRow(RowDefinition row, IResizablePanelViewModel panelViewModel)
    {
        void ApplyCollapseState() =>
            row.Height = panelViewModel.IsCollapsed
                ? GridLength.Auto
                : new GridLength(panelViewModel.Height + CollapsiblePanel.HeaderHeight, GridUnitType.Pixel);

        ApplyCollapseState();

        void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IResizablePanelViewModel.IsCollapsed))
            {
                ApplyCollapseState();
            }
        }

        void OnRowHeightChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == RowDefinition.HeightProperty && !panelViewModel.IsCollapsed && row.Height.IsAbsolute)
            {
                panelViewModel.Height = Math.Max(0, row.Height.Value - CollapsiblePanel.HeaderHeight);
            }
        }

        panelViewModel.PropertyChanged += OnViewModelPropertyChanged;
        row.PropertyChanged += OnRowHeightChanged;

        _unsubscribers.Add(() => panelViewModel.PropertyChanged -= OnViewModelPropertyChanged);
        _unsubscribers.Add(() => row.PropertyChanged -= OnRowHeightChanged);
    }
}
