using System.Text.RegularExpressions;
using LimelightX.UI.ViewModels;
using Xunit;

namespace LimelightX.UI.Tests.About;

/// <summary>
/// AboutViewModel's own state/behavior (ui-viewmodels.md §10) - gating
/// tests (never blocked by IExecutionLockService, IsAboutOpen open/close)
/// live in Workspace/WorkspaceViewModelTests.cs alongside the equivalent
/// Settings gating tests, since that state lives on WorkspaceViewModel.
/// </summary>
public class AboutViewModelTests
{
    [Fact]
    public void AboutViewModel_ExposesStaticProjectInfo()
    {
        var about = new AboutViewModel();

        Assert.Equal("Limelight-X", about.AppName);
        Assert.Contains("deterministic expression layer", about.Description);
        Assert.Equal("https://github.com/dondemcsak/limelight-x", about.GitHubUrl);
    }

    [Fact]
    public void Version_IsMajorMinorPatchFormat()
    {
        var about = new AboutViewModel();

        Assert.Matches(new Regex(@"^\d+\.\d+\.\d+$"), about.Version);
    }

    [Fact]
    public void CloseCommand_InvokesCloseRequested()
    {
        var about = new AboutViewModel();
        var closed = false;
        about.CloseRequested = () => closed = true;

        about.CloseCommand.Execute(null);

        Assert.True(closed);
    }
}
