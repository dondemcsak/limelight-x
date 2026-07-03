using System.ComponentModel;
using Avalonia.Controls;
using LimelightX.UI.ViewModels.Inspectors;

namespace LimelightX.UI.Components;

/// <summary>Final result rendering, same style as ModelOutputPanel (ui-components.md §4.6).</summary>
public partial class FinalResultPanel : UserControl
{
    private FinalResultViewModel? _subscribedViewModel;

    public FinalResultPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachViewModel();
    }

    private void AttachViewModel()
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (DataContext is FinalResultViewModel viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _subscribedViewModel = viewModel;
            Render(viewModel);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is FinalResultViewModel viewModel &&
            e.PropertyName is nameof(FinalResultViewModel.ResultText) or nameof(FinalResultViewModel.ContentType))
        {
            Render(viewModel);
        }
    }

    private void Render(FinalResultViewModel viewModel)
    {
        ResultHost.Content = ContentRenderer.Render(viewModel.ResultText, viewModel.ContentType);
    }
}
