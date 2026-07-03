using Avalonia.Controls;
using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Views;

public partial class EditorPage : UserControl
{
    private EditorViewModel? _subscribedViewModel;

    public EditorPage()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachUndoRedoBridge();
    }

    private void AttachUndoRedoBridge()
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.UndoRequested -= Editor.Undo;
            _subscribedViewModel.RedoRequested -= Editor.Redo;
        }

        if (DataContext is EditorViewModel viewModel)
        {
            viewModel.UndoRequested += Editor.Undo;
            viewModel.RedoRequested += Editor.Redo;
            _subscribedViewModel = viewModel;
        }
    }
}
