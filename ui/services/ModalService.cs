using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace LimelightX.UI.Services;

/// <summary>
/// Builds dialog windows in code rather than .axaml views, since Phase 7
/// replaces this with the fully styled modal (ui-error-handling.md §5) and
/// there is no point maintaining parallel XAML for a stub.
/// </summary>
public sealed class ModalService(Func<Window?> ownerAccessor) : IModalService
{
    public Task ShowBlockedNavigationAsync(string reason) =>
        ShowAsync("Navigation Blocked", reason, [("OK", true)], errorBorder: false);

    public Task<bool> ShowUnsavedChangesConfirmationAsync() =>
        ShowAsync(
            "Unsaved Changes",
            "You have unsaved settings changes. Discard them?",
            [("Stay", false), ("Discard Changes", true)],
            errorBorder: false);

    public Task ShowFatalErrorAsync(string message) =>
        ShowAsync("Error", message, [("OK", true)], errorBorder: true);

    private async Task<bool> ShowAsync(string title, string message, (string Label, bool Result)[] buttons, bool errorBorder)
    {
        var owner = ownerAccessor();
        if (owner is null)
        {
            return false;
        }

        var result = false;
        var dialog = new Window
        {
            Title = title,
            Width = 380,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (IBrush?)owner.FindResource("SurfaceBrush"),
        };

        if (errorBorder)
        {
            dialog.BorderBrush = (IBrush?)owner.FindResource("StatusErrorBrush");
            dialog.BorderThickness = new Avalonia.Thickness(1);
        }

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var (label, buttonResult) in buttons)
        {
            var button = new Button { Content = label, Classes = { buttonResult ? "primary" : "secondary" } };
            button.Click += (_, _) =>
            {
                result = buttonResult;
                dialog.Close();
            };
            buttonPanel.Children.Add(button);
        }

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (IBrush?)owner.FindResource("TextPrimaryBrush"),
                },
                buttonPanel,
            },
        };

        await dialog.ShowDialog(owner);
        return result;
    }
}
