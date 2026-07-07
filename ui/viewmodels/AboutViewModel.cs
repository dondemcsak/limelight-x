using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LimelightX.UI.ViewModels;

/// <summary>
/// Backs the About modal (ui-viewmodels.md §10, ui-components.md §7.2) -
/// a composition-root singleton, not file-scoped, mirroring
/// SettingsViewModel. Unlike Settings, About has no editable state and no
/// backend dependency - it is never gated by IExecutionLockService.
/// </summary>
public partial class AboutViewModel : ObservableObject
{
    public string AppName { get; } = "Limelight-X";

    public string Description { get; } =
        "Limelight-X is a minimal, deterministic expression layer that compiles a small Constrained Natural Language (CNL) " +
        "into a linear Intermediate Representation (IR) and evaluates it using a combination of local logic and a Claude 3.5 Sonnet model adapter.";

    public string Version { get; } = FormatVersion(Assembly.GetExecutingAssembly().GetName().Version);

    public string GitHubUrl { get; } = "https://github.com/dondemcsak/limelight-x";

    /// <summary>Set by the composition root to WorkspaceViewModel.CloseAboutCommand (App.axaml.cs) - keeps this class free of a compile-time WorkspaceViewModel dependency.</summary>
    public Action? CloseRequested { get; set; }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    [RelayCommand]
    private void OpenGitHub() => Process.Start(new ProcessStartInfo(GitHubUrl) { UseShellExecute = true });

    private static string FormatVersion(Version? version) =>
        version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
}
