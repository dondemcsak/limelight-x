using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
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
/// LocalDiagnosticsRenderer/DiagnosticMarginMarker for advisory error spans
/// (bdd-ui-interactions.md §2.16), a FoldingManager for sentence folding
/// (§2.9), a GhostTextElementGenerator for inline suggested-fix ghost text
/// (§2.18), and a Ctrl+Space-triggered CompletionWindow (§2.12-§2.13).
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

    public static readonly StyledProperty<IEnumerable<LocalDiagnostic>?> LocalDiagnosticsProperty =
        AvaloniaProperty.Register<CnlEditor, IEnumerable<LocalDiagnostic>?>(nameof(LocalDiagnostics));

    public static readonly StyledProperty<IEnumerable<FoldRegion>?> FoldRegionsProperty =
        AvaloniaProperty.Register<CnlEditor, IEnumerable<FoldRegion>?>(nameof(FoldRegions));

    public static readonly StyledProperty<QuickFixItem?> GhostSuggestionProperty =
        AvaloniaProperty.Register<CnlEditor, QuickFixItem?>(nameof(GhostSuggestion));

    private readonly CnlSyntaxColorizer _colorizer;
    private readonly LocalDiagnosticsRenderer _diagnosticsRenderer;
    private readonly DiagnosticMarginMarker _marginMarker;
    private readonly GhostTextElementGenerator _ghostTextGenerator;
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

        _marginMarker = new DiagnosticMarginMarker(((SolidColorBrush)GetBrush("SyntaxErrorBrush")).Color);
        Editor.TextArea.LeftMargins.Add(_marginMarker);

        var ghostTextBrush = new SolidColorBrush(((SolidColorBrush)GetBrush("TextPrimaryBrush")).Color, 0.4);
        _ghostTextGenerator = new GhostTextElementGenerator(ghostTextBrush);
        Editor.TextArea.TextView.ElementGenerators.Add(_ghostTextGenerator);

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

        // Tunnel (preview), not bubble: TextArea.OnKeyDown is a class handler
        // that claims Tab for indent-insertion before any bubble-phase
        // instance handler on this same node would run. Intercepting at the
        // tunnel phase lets Tab-to-accept (bdd-ui-interactions.md §2.19) win
        // when a ghost suggestion is active, while leaving the event
        // unhandled otherwise so it still falls through to that default
        // indent behavior (ui-accessibility.md §2).
        Editor.TextArea.AddHandler(InputElement.KeyDownEvent, OnTextAreaKeyDown, RoutingStrategies.Tunnel);

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
        if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.None)
        {
            OnGhostTextAcceptRequested(e);
        }
        else if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.Control)
        {
            OnCompletionRequested(e);
        }
        else if (e.Key == Key.Right && e.KeyModifiers == (KeyModifiers.Alt | KeyModifiers.Shift))
        {
            OnExpandSelectionRequested(e);
        }
    }

    /// <summary>
    /// Tab commits the active ghost-text suggestion (bdd-ui-interactions.md
    /// §2.19); when none is active, the event is left unhandled so it falls
    /// through to AvaloniaEdit's existing default indent-insert behavior
    /// (ui-accessibility.md §2's editor-scoped Tab override).
    /// </summary>
    private void OnGhostTextAcceptRequested(KeyEventArgs e)
    {
        if (DataContext is not CnlTabViewModel tab || tab.Editor.GhostSuggestion is not { } ghost)
        {
            return;
        }

        e.Handled = true;
        tab.Editor.ApplyQuickFixCommand.Execute(ghost);
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

    public QuickFixItem? GhostSuggestion
    {
        get => GetValue(GhostSuggestionProperty);
        set => SetValue(GhostSuggestionProperty, value);
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
        else if (change.Property == GhostSuggestionProperty)
        {
            UpdateGhostText();
        }
    }

    private void OnLocalDiagnosticsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateDiagnosticsRenderer();

    private void OnFoldRegionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateFoldings();

    private void UpdateDiagnosticsRenderer()
    {
        var utf8Text = new Utf8Text(Editor.Text);
        var diagnostics = (LocalDiagnostics ?? [])
            .Select(d => (utf8Text.ByteOffsetToCharOffset(d.StartByte), utf8Text.ByteOffsetToCharOffset(d.EndByte)))
            .ToList();

        _diagnosticsRenderer.Diagnostics = diagnostics;
        Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);

        _marginMarker.Diagnostics = diagnostics;
        _marginMarker.InvalidateVisual();
    }

    /// <summary>Feeds GhostSuggestion (bdd-ui-interactions.md §2.18) into GhostTextElementGenerator, converting InsertionByte to a char offset, and forces the TextView to reconstruct visual lines so the injected element appears/disappears immediately.</summary>
    private void UpdateGhostText()
    {
        if (GhostSuggestion is { } ghost)
        {
            var utf8Text = new Utf8Text(Editor.Text);
            _ghostTextGenerator.InsertionOffset = utf8Text.ByteOffsetToCharOffset(ghost.InsertionByte);
            _ghostTextGenerator.InsertText = ghost.InsertText;
        }
        else
        {
            _ghostTextGenerator.InsertionOffset = null;
            _ghostTextGenerator.InsertText = string.Empty;
        }

        Editor.TextArea.TextView.Redraw();
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
