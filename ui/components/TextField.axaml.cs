using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace LimelightX.UI.Components;

/// <summary>Standard single-line text input (ui-components.md §5.6).</summary>
public partial class TextField : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<TextField, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<TextField, string>(nameof(Value), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> ValidationErrorProperty =
        AvaloniaProperty.Register<TextField, string?>(nameof(ValidationError));

    public TextField()
    {
        InitializeComponent();
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? ValidationError
    {
        get => GetValue(ValidationErrorProperty);
        set => SetValue(ValidationErrorProperty, value);
    }
}
