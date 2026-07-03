using Avalonia;
using Avalonia.Controls;

namespace LimelightX.UI.Components;

/// <summary>Loading state during backend calls (ui-components.md §5.3).</summary>
public partial class LoadingIndicator : UserControl
{
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<LoadingIndicator, bool>(nameof(IsLoading));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<LoadingIndicator, string?>(nameof(Text));

    public LoadingIndicator()
    {
        InitializeComponent();
    }

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
