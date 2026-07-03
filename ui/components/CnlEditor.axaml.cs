using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;

namespace LimelightX.UI.Components;

/// <summary>
/// CNL text editor (ui-components.md §3.1) wrapping AvaloniaEdit's
/// TextEditor. Two-way syncs Text/CursorPosition/SelectionRange with
/// EditorViewModel; installs CnlSyntaxColorizer for syntax highlighting.
/// ValidationOverlay (inline error markers) is a separate component composed
/// alongside this one in EditorPage, not owned by CnlEditor itself.
/// </summary>
public partial class CnlEditor : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<CnlEditor, string>(nameof(Text), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> CursorPositionProperty =
        AvaloniaProperty.Register<CnlEditor, int>(nameof(CursorPosition), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<(int Start, int End)> SelectionRangeProperty =
        AvaloniaProperty.Register<CnlEditor, (int Start, int End)>(nameof(SelectionRange), defaultBindingMode: BindingMode.TwoWay);

    private bool _isSyncingFromViewModel;

    public CnlEditor()
    {
        InitializeComponent();

        var brushes = new Dictionary<TokenKind, IBrush>
        {
            [TokenKind.Keyword] = GetBrush("SyntaxKeywordBrush"),
            [TokenKind.Pronoun] = GetBrush("SyntaxPronounBrush"),
            [TokenKind.Resource] = GetBrush("SyntaxResourceBrush"),
            [TokenKind.ExpressionHole] = GetBrush("SyntaxExpressionHoleBrush"),
            [TokenKind.String] = GetBrush("SyntaxStringBrush"),
        };

        Editor.TextArea.TextView.LineTransformers.Add(new CnlSyntaxColorizer(brushes));

        Editor.TextChanged += (_, _) =>
        {
            if (_isSyncingFromViewModel)
            {
                return;
            }

            SetCurrentValue(TextProperty, Editor.Text);
        };

        Editor.TextArea.Caret.PositionChanged += (_, _) =>
            SetCurrentValue(CursorPositionProperty, Editor.CaretOffset);

        Editor.TextArea.SelectionChanged += (_, _) =>
            SetCurrentValue(SelectionRangeProperty, (Editor.SelectionStart, Editor.SelectionStart + Editor.SelectionLength));
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public int CursorPosition
    {
        get => GetValue(CursorPositionProperty);
        set => SetValue(CursorPositionProperty, value);
    }

    public (int Start, int End) SelectionRange
    {
        get => GetValue(SelectionRangeProperty);
        set => SetValue(SelectionRangeProperty, value);
    }

    public void Undo() => Editor.Undo();

    public void Redo() => Editor.Redo();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            var newText = change.GetNewValue<string>();
            if (Editor.Text == newText)
            {
                return;
            }

            _isSyncingFromViewModel = true;
            Editor.Text = newText;
            _isSyncingFromViewModel = false;
        }
    }

    private static IBrush GetBrush(string resourceKey)
    {
        return Application.Current?.FindResource(resourceKey) as IBrush
            ?? throw new InvalidOperationException($"Resource '{resourceKey}' not found.");
    }
}
