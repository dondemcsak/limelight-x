using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace LimelightX.UI.Components;

/// <summary>Labeled dropdown selector (ui-components.md §5.8), used by SettingsPage for the Dev/Stage/Prod environment profile.</summary>
public partial class SelectField : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SelectField, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<IEnumerable?> OptionsProperty =
        AvaloniaProperty.Register<SelectField, IEnumerable?>(nameof(Options));

    public static readonly StyledProperty<object?> SelectedValueProperty =
        AvaloniaProperty.Register<SelectField, object?>(nameof(SelectedValue), defaultBindingMode: BindingMode.TwoWay);

    public SelectField()
    {
        InitializeComponent();
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public IEnumerable? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }
}
