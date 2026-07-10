using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;
using LimelightX.UI.Intellisense;
using LimelightX.UI.ViewModels;
using LimelightX.UI.ViewModels.Tabs;

namespace LimelightX.UI.Components;

/// <summary>
/// CNL text editor (ui-components.md §3.1) wrapping AvaloniaEdit's
/// TextEditor. Two-way syncs Text/CursorPosition/SelectionRange with
/// EditorViewModel; installs CnlSyntaxColorizer for syntax highlighting,
/// LocalDiagnosticsRenderer for advisory error spans (bdd-ui-interactions.md
/// §2.7-§2.8), a FoldingManager for sentence folding (§2.9), and a
/// Ctrl+Space-triggered CompletionWindow (§2.12-§2.13). ValidationOverlay
/// (inline error markers) is a separate component composed alongside this
/// one in EditorPage, not owned by CnlEditor itself.
/// </summary>
public partial class CnlEditor : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<CnlEditor, string>(nameof(Text), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> CursorPositionProperty =
        AvaloniaProperty.Register<CnlEditor, int>(nameof(CursorPosition), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<(int Start, int End)> SelectionRangeProperty =
        AvaloniaProperty.Register<CnlEditor, (int Start, int End)>(nameof(SelectionRange), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IEnumerable<LocalDiagnostic>?> LocalDiagnosticsProperty =
        AvaloniaProperty.Register<CnlEditor, IEnumerable<LocalDiagnostic>?>(nameof(LocalDiagnostics));

    public static readonly StyledProperty<IEnumerable<FoldRegion>?> FoldRegionsProperty =
        AvaloniaProperty.Register<CnlEditor, IEnumerable<FoldRegion>?>(nameof(FoldRegions));

    private readonly CnlSyntaxColorizer _colorizer;
    private readonly LocalDiagnosticsRenderer _diagnosticsRenderer;
    private readonly FoldingManager _foldingManager;
    private CompletionWindow? _completionWindow;
    private bool _isSyncingFromViewModel;
    private bool _isSyncingSelectionFromViewModel;

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

        _colorizer = new CnlSyntaxColorizer(brushes);
        Editor.TextArea.TextView.LineTransformers.Add(_colorizer);

        _diagnosticsRenderer = new LocalDiagnosticsRenderer(((SolidColorBrush)GetBrush("SyntaxErrorBrush")).Color);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_diagnosticsRenderer);

        _foldingManager = FoldingManager.Install(Editor.TextArea);

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
        {
            if (_isSyncingSelectionFromViewModel)
            {
                return;
            }

            SetCurrentValue(SelectionRangeProperty, (Editor.SelectionStart, Editor.SelectionStart + Editor.SelectionLength));
        };

        Editor.TextArea.KeyDown += OnTextAreaKeyDown;

        ToolTip.SetServiceEnabled(Editor, false);
        Editor.PointerHover += OnPointerHover;
        Editor.PointerHoverStopped += OnPointerHoverStopped;
    }

    /// <summary>
    /// Pointer-driven hover (bdd-ui-interactions.md §2.11) - Avalonia's own
    /// hover-triggered ToolTip service is disabled (ToolTip.SetServiceEnabled
    /// above) since this drives IsOpen manually from
    /// EditorViewModel.HoverInfo instead. No built-in AvaloniaEdit tooltip
    /// widget exists, so the standard Avalonia ToolTip attached property is
    /// reused as the popup rather than a hand-rolled Popup control.
    /// </summary>
    private void OnPointerHover(object? sender, PointerEventArgs e)
    {
        if (DataContext is not CnlTabViewModel tab)
        {
            return;
        }

        var position = Editor.GetPositionFromPoint(e.GetPosition(Editor));
        if (position is null)
        {
            tab.Editor.ClearHover();
            ToolTip.SetIsOpen(Editor, false);
            return;
        }

        var cursorChar = Editor.Document.GetOffset(position.Value.Location);
        var utf8Text = new Utf8Text(Editor.Text);
        tab.Editor.RequestHoverAt(utf8Text.CharOffsetToByteOffset(cursorChar));

        if (tab.Editor.HoverInfo is { } hover)
        {
            ToolTip.SetTip(Editor, hover.Text);
            ToolTip.SetIsOpen(Editor, true);
        }
        else
        {
            ToolTip.SetIsOpen(Editor, false);
        }
    }

    private void OnPointerHoverStopped(object? sender, PointerEventArgs e)
    {
        if (DataContext is CnlTabViewModel tab)
        {
            tab.Editor.ClearHover();
        }

        ToolTip.SetIsOpen(Editor, false);
    }

    /// <summary>
    /// Ctrl+Space triggers completion (§2.12-§2.13); Alt+Shift+Right triggers
    /// structural selection expansion (§2.10, matching VS Code's binding for
    /// the same concept). Both reach EditorViewModel via DataContext
    /// (CnlTabView.axaml binds this control's individual properties, not
    /// DataContext itself, so DataContext still inherits CnlTabViewModel from
    /// the logical tree) - the same pattern OnPointerHover above uses.
    /// </summary>
    private void OnTextAreaKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.Control)
        {
            OnCompletionRequested(e);
        }
        else if (e.Key == Key.Right && e.KeyModifiers == (KeyModifiers.Alt | KeyModifiers.Shift))
        {
            OnExpandSelectionRequested(e);
        }
    }

    private void OnExpandSelectionRequested(KeyEventArgs e)
    {
        if (DataContext is not CnlTabViewModel tab)
        {
            return;
        }

        e.Handled = true;
        tab.Editor.ExpandSelection();
    }

    private void OnCompletionRequested(KeyEventArgs e)
    {
        if (DataContext is not CnlTabViewModel tab)
        {
            return;
        }

        e.Handled = true;

        var utf8Text = new Utf8Text(Editor.Text);
        var cursorByte = utf8Text.CharOffsetToByteOffset(Editor.CaretOffset);
        tab.Editor.RequestCompletionsAt(cursorByte);

        if (tab.Editor.CompletionItems.Count == 0)
        {
            return;
        }

        _completionWindow = new CompletionWindow(Editor.TextArea);
        foreach (var item in tab.Editor.CompletionItems)
        {
            _completionWindow.CompletionList.CompletionData.Add(new CnlCompletionData(item));
        }

        _completionWindow.Closed += (_, _) => _completionWindow = null;
        _completionWindow.Show();
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

    public IEnumerable<LocalDiagnostic>? LocalDiagnostics
    {
        get => GetValue(LocalDiagnosticsProperty);
        set => SetValue(LocalDiagnosticsProperty, value);
    }

    public IEnumerable<FoldRegion>? FoldRegions
    {
        get => GetValue(FoldRegionsProperty);
        set => SetValue(FoldRegionsProperty, value);
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
        else if (change.Property == SelectionRangeProperty)
        {
            var (start, end) = change.GetNewValue<(int Start, int End)>();
            if (Editor.SelectionStart == start && Editor.SelectionLength == end - start)
            {
                return;
            }

            _isSyncingSelectionFromViewModel = true;
            Editor.Select(start, end - start);
            _isSyncingSelectionFromViewModel = false;
        }
        else if (change.Property == LocalDiagnosticsProperty)
        {
            if (change.GetOldValue<IEnumerable<LocalDiagnostic>?>() is INotifyCollectionChanged oldNotifying)
            {
                oldNotifying.CollectionChanged -= OnLocalDiagnosticsCollectionChanged;
            }

            if (change.GetNewValue<IEnumerable<LocalDiagnostic>?>() is INotifyCollectionChanged newNotifying)
            {
                newNotifying.CollectionChanged += OnLocalDiagnosticsCollectionChanged;
            }

            UpdateDiagnosticsRenderer();
        }
        else if (change.Property == FoldRegionsProperty)
        {
            if (change.GetOldValue<IEnumerable<FoldRegion>?>() is INotifyCollectionChanged oldNotifying)
            {
                oldNotifying.CollectionChanged -= OnFoldRegionsCollectionChanged;
            }

            if (change.GetNewValue<IEnumerable<FoldRegion>?>() is INotifyCollectionChanged newNotifying)
            {
                newNotifying.CollectionChanged += OnFoldRegionsCollectionChanged;
            }

            UpdateFoldings();
        }
    }

    private void OnLocalDiagnosticsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateDiagnosticsRenderer();

    private void OnFoldRegionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateFoldings();

    private void UpdateDiagnosticsRenderer()
    {
        var utf8Text = new Utf8Text(Editor.Text);
        _diagnosticsRenderer.Diagnostics = (LocalDiagnostics ?? [])
            .Select(d => (utf8Text.ByteOffsetToCharOffset(d.StartByte), utf8Text.ByteOffsetToCharOffset(d.EndByte)))
            .ToList();
        Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
    }

    private void UpdateFoldings()
    {
        var utf8Text = new Utf8Text(Editor.Text);
        var newFoldings = (FoldRegions ?? [])
            .Select(f => new NewFolding(utf8Text.ByteOffsetToCharOffset(f.StartByte), utf8Text.ByteOffsetToCharOffset(f.EndByte)))
            .OrderBy(f => f.StartOffset)
            .ToList();

        _foldingManager.UpdateFoldings(newFoldings, -1);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _colorizer.Dispose();
        FoldingManager.Uninstall(_foldingManager);
        _completionWindow?.Close();
    }

    private static IBrush GetBrush(string resourceKey)
    {
        return Application.Current?.FindResource(resourceKey) as IBrush
            ?? throw new InvalidOperationException($"Resource '{resourceKey}' not found.");
    }
}
