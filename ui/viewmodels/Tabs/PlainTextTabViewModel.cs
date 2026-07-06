using System.ComponentModel;

namespace LimelightX.UI.ViewModels.Tabs;

/// <summary>Tab for a non-.llx file (ui-viewmodels.md §5.3) - owns one PlainTextEditorViewModel, nothing else.</summary>
public sealed class PlainTextTabViewModel : TabViewModel
{
    public PlainTextTabViewModel(string filePath, string initialText)
        : base(filePath)
    {
        Editor = new PlainTextEditorViewModel { Text = initialText };
        Editor.PropertyChanged += OnEditorPropertyChanged;
    }

    public PlainTextEditorViewModel Editor { get; }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlainTextEditorViewModel.Text))
        {
            IsDirty = true;
        }
    }

    public override void Dispose() => Editor.PropertyChanged -= OnEditorPropertyChanged;
}
