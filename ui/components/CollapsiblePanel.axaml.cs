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
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<CollapsiblePanel, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<bool> IsCollapsedProperty =
        AvaloniaProperty.Register<CollapsiblePanel, bool>(nameof(IsCollapsed), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<object?> PanelContentProperty =
        AvaloniaProperty.Register<CollapsiblePanel, object?>(nameof(PanelContent));

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
