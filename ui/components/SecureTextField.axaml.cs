using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;

namespace LimelightX.UI.Components;

/// <summary>Masked text input for secrets (ui-components.md §5.7), used by SettingsPage for ANTHROPIC_API_KEY.</summary>
public partial class SecureTextField : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SecureTextField, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<SecureTextField, string>(nameof(Value), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> ValidationErrorProperty =
        AvaloniaProperty.Register<SecureTextField, string?>(nameof(ValidationError));

    public static readonly StyledProperty<bool> IsValueVisibleProperty =
        AvaloniaProperty.Register<SecureTextField, bool>(nameof(IsValueVisible));

    public static readonly DirectProperty<SecureTextField, char> MaskCharProperty =
        AvaloniaProperty.RegisterDirect<SecureTextField, char>(nameof(MaskChar), o => o.MaskChar);

    public static readonly DirectProperty<SecureTextField, Symbol> ToggleIconSymbolProperty =
        AvaloniaProperty.RegisterDirect<SecureTextField, Symbol>(nameof(ToggleIconSymbol), o => o.ToggleIconSymbol);

    public static readonly DirectProperty<SecureTextField, string> ToggleAccessibleNameProperty =
        AvaloniaProperty.RegisterDirect<SecureTextField, string>(nameof(ToggleAccessibleName), o => o.ToggleAccessibleName);

    public SecureTextField()
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

    public bool IsValueVisible
    {
        get => GetValue(IsValueVisibleProperty);
        set => SetValue(IsValueVisibleProperty, value);
    }

    public char MaskChar => IsValueVisible ? '\0' : '•';

    public Symbol ToggleIconSymbol => IsValueVisible ? Symbol.EyeOff : Symbol.Eye;

    public string ToggleAccessibleName => IsValueVisible ? "Hide API key" : "Show API key";

    [RelayCommand]
    private void ToggleVisibility() => IsValueVisible = !IsValueVisible;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != IsValueVisibleProperty)
        {
            return;
        }

        var wasVisible = change.GetOldValue<bool>();
        var isVisible = change.GetNewValue<bool>();

        RaisePropertyChanged(MaskCharProperty, wasVisible ? '\0' : '•', isVisible ? '\0' : '•');
        RaisePropertyChanged(ToggleIconSymbolProperty, wasVisible ? Symbol.EyeOff : Symbol.Eye, isVisible ? Symbol.EyeOff : Symbol.Eye);
        RaisePropertyChanged(ToggleAccessibleNameProperty, wasVisible ? "Hide API key" : "Show API key", isVisible ? "Hide API key" : "Show API key");
    }
}
