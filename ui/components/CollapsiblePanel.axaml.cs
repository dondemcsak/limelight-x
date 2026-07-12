using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;

namespace LimelightX.UI.Components;

/// <summary>
/// Reusable inspector section chrome (ui-components.md §5.4): header +
/// chevron + toggle + content. Used as the base for RawAstPanel,
/// NormalizedAstPanel, IrPanel, PromptPanel, ModelOutputPanel,
/// FinalResultPanel. Stateless except the ephemeral collapse/expand it
/// mirrors from IsCollapsed (ui-components.md §1 rule).
/// </summary>
public partial class CollapsiblePanel : UserControl
{
    /// <summary>
    /// Rendered height of the header button (Classes="icon", cardHeader text,
    /// PaddingMediumThickness), empirically measured via a headless render
    /// (constant across all six panel titles - only text width varies, not
    /// height). CnlTabView's accordion Grid row must reserve this on top of
    /// PanelHeight when expanded, or the panel's total rendered height
    /// (header + ContentScrollViewer) overflows the row and the bottom of
    /// the content - including part of its own scrollbar - gets clipped by
    /// the Grid cell, independent of whether ContentScrollViewer's own
    /// Extent/Offset are correct (ui-components.md §5.1 Layout Rules).
    /// </summary>
    public const double HeaderHeight = 42.0;

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<CollapsiblePanel, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<bool> IsCollapsedProperty =
        AvaloniaProperty.Register<CollapsiblePanel, bool>(nameof(IsCollapsed), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<object?> PanelContentProperty =
        AvaloniaProperty.Register<CollapsiblePanel, object?>(nameof(PanelContent));

    /// <summary>
    /// This panel's current expanded height (ui-components.md §5.1). NaN
    /// (the default) means auto-size to content, unconstrained - callers that
    /// want the fixed-height/internal-scroll resize behavior bind this to
    /// their owning inspector ViewModel's Height property (TwoWay, so
    /// dragging this panel's splitter handle in CnlTabView writes back).
    /// </summary>
    public static readonly StyledProperty<double> PanelHeightProperty =
        AvaloniaProperty.Register<CollapsiblePanel, double>(nameof(PanelHeight), double.NaN, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly DirectProperty<CollapsiblePanel, Symbol> ChevronSymbolProperty =
        AvaloniaProperty.RegisterDirect<CollapsiblePanel, Symbol>(nameof(ChevronSymbol), o => o.ChevronSymbol);

    public static readonly DirectProperty<CollapsiblePanel, string> AccessibleNameProperty =
        AvaloniaProperty.RegisterDirect<CollapsiblePanel, string>(nameof(AccessibleName), o => o.AccessibleName);

    private Symbol _chevronSymbol = Symbol.ChevronDown;

    public CollapsiblePanel()
    {
        InitializeComponent();
        UpdateChevron();
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool IsCollapsed
    {
        get => GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    public object? PanelContent
    {
        get => GetValue(PanelContentProperty);
        set => SetValue(PanelContentProperty, value);
    }

    public double PanelHeight
    {
        get => GetValue(PanelHeightProperty);
        set => SetValue(PanelHeightProperty, value);
    }

    public Symbol ChevronSymbol
    {
        get => _chevronSymbol;
        private set => SetAndRaise(ChevronSymbolProperty, ref _chevronSymbol, value);
    }

    /// <summary>
    /// ui-accessibility.md §13: collapsible headers expose role="button" (the
    /// header already is a real Button) and aria-expanded. Avalonia has no
    /// direct aria-expanded equivalent for a plain Button, so the
    /// expanded/collapsed state is folded into the accessible name instead.
    /// </summary>
    public string AccessibleName => $"{Title}, {(IsCollapsed ? "collapsed" : "expanded")}";

    [RelayCommand]
    private void Toggle() => IsCollapsed = !IsCollapsed;

    /// <summary>
    /// Scrolls this panel's own content area so entryContainer's top edge
    /// lands at the top of the panel's visible viewport (ui-components.md
    /// §5.5-§5.6, bdd-ui-interactions.md §4.12-§4.13: PromptPanel/
    /// ModelOutputPanel position each newly appended entry at the top,
    /// unconditionally). The panel's content is the thing that actually
    /// scrolls (Height-bound ScrollViewer, above) - callers (e.g.
    /// PromptPanel) never need their own nested ScrollViewer.
    /// </summary>
    public void ScrollContentToTopOf(Control entryContainer)
    {
        var topLeft = entryContainer.TranslatePoint(new Point(0, 0), ContentScrollViewer) ?? default;
        var target = Math.Max(0, ContentScrollViewer.Offset.Y + topLeft.Y);
        ContentScrollViewer.Offset = new Vector(ContentScrollViewer.Offset.X, target);
    }

    /// <summary>Read-only scroll-state accessors for tests asserting bdd-ui-interactions.md §4.12-§4.13, §4.17-§4.18.</summary>
    public Size ContentExtent => ContentScrollViewer.Extent;

    public Size ContentViewport => ContentScrollViewer.Viewport;

    public Vector ContentOffset
    {
        get => ContentScrollViewer.Offset;
        set => ContentScrollViewer.Offset = value;
    }

    /// <summary>Position of the given descendant relative to this panel's content viewport - e.g. (0, 0) means its top-left is exactly at the top of the visible area.</summary>
    public Point TranslateToViewport(Control descendant) => descendant.TranslatePoint(new Point(0, 0), ContentScrollViewer) ?? default;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsCollapsedProperty)
        {
            UpdateChevron();
            RaisePropertyChanged(AccessibleNameProperty, string.Empty, AccessibleName);
        }
        else if (change.Property == TitleProperty)
        {
            RaisePropertyChanged(AccessibleNameProperty, string.Empty, AccessibleName);
        }
    }

    private void UpdateChevron() => ChevronSymbol = IsCollapsed ? Symbol.ChevronRight : Symbol.ChevronDown;
}
