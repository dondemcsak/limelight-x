using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
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
    /// the ViewModel, updated as the user drags this row's GridSplitter.
    /// </summary>
    private void BindPanelRow(RowDefinition row, IResizablePanelViewModel panelViewModel)
    {
        void ApplyCollapseState() =>
            row.Height = panelViewModel.IsCollapsed
                ? GridLength.Auto
                : new GridLength(panelViewModel.Height, GridUnitType.Pixel);

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
                panelViewModel.Height = row.Height.Value;
            }
        }

        panelViewModel.PropertyChanged += OnViewModelPropertyChanged;
        row.PropertyChanged += OnRowHeightChanged;

        _unsubscribers.Add(() => panelViewModel.PropertyChanged -= OnViewModelPropertyChanged);
        _unsubscribers.Add(() => row.PropertyChanged -= OnRowHeightChanged);
    }
}
