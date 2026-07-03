using Avalonia;
using Avalonia.Headless;
using LimelightX.UI;
using LimelightX.UI.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace LimelightX.UI.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
