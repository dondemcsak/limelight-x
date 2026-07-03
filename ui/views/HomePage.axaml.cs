using Avalonia.Controls;
using LimelightX.UI.Routing;

namespace LimelightX.UI.Views;

public partial class HomePage : UserControl
{
    public HomePage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Set by MainWindow alongside DataContext (FileLoaderViewModel) so the
    /// gear icon can navigate without FileLoaderViewModel needing a
    /// NavigationViewModel dependency of its own.
    /// </summary>
    public static readonly Avalonia.StyledProperty<NavigationViewModel?> NavigationProperty =
        Avalonia.AvaloniaProperty.Register<HomePage, NavigationViewModel?>(nameof(Navigation));

    public NavigationViewModel? Navigation
    {
        get => GetValue(NavigationProperty);
        set => SetValue(NavigationProperty, value);
    }
}
