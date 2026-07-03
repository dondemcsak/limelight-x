using LimelightX.UI.Services;
using Xunit;

namespace LimelightX.UI.Tests.Settings;

/// <summary>
/// Manual verification against the real llx.exe binary (not part of the
/// regular mock-only suite) - confirms StartAsync's "Listening on http://"
/// detection and StopAsync's shutdown path against the actual process.
/// Skips gracefully if the binary isn't present at the expected path.
/// </summary>
public class LlxProcessServiceLiveTests
{
    private const string ExecutablePath = @"C:\Code\limelight-private\target\release\llx.exe";

    [Fact]
    public async Task StartAsync_RealBinary_DetectsListeningAndStopsCleanly()
    {
        if (!File.Exists(ExecutablePath))
        {
            return;
        }

        var service = new LlxProcessService(ExecutablePath);

        var outcome = await service.StartAsync(4748, "dummy-key-for-startup-only");

        Assert.True(outcome.Success, outcome.ErrorMessage);
        Assert.True(service.IsRunning);

        await service.StopAsync();

        Assert.False(service.IsRunning);
    }
}
