using System.ComponentModel;

namespace LimelightX.UI.ViewModels.Tabs;

/// <summary>Tab for a non-.llx file (ui-viewmodels.md §5.3) - owns one PlainTextEditorViewModel, nothing else.</summary>
public sealed class PlainTextTabViewModel : TabViewModel
{
    public PlainTextTabViewModel(string filePath, string initialText)
        : base(filePath, Path.GetFileName(filePath))
    {
        Editor = new PlainTextEditorViewModel { Text = initialText };
        Editor.PropertyChanged += OnEditorPropertyChanged;
    }

    /// <summary>Untitled-tab constructor (File > New TXT File, ui-viewmodels.md §3) - no backing file, starts with empty text and IsDirty == false.</summary>
    public PlainTextTabViewModel(string header)
        : base(null, header)
    {
        Editor = new PlainTextEditorViewModel { Text = string.Empty };
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
