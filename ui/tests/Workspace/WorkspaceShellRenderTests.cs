using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LimelightX.UI.Components;
using LimelightX.UI.Services;
using LimelightX.UI.ViewModels.Tabs;
using LimelightX.UI.ViewModels.Workspace;
using Xunit;

namespace LimelightX.UI.Tests.Workspace;

/// <summary>
/// Headless render smoke tests for the Phase 4 workspace components
/// (FileTreeView, TabStrip, TabContentHost) - compiled bindings already
/// catch most path/type errors at build time, but resource lookups
/// (StaticResource), DataTemplate selection by runtime type, and the
/// TreeViewItem IsExpanded style binding are only exercised by actually
/// constructing and attaching these controls.
/// </summary>
public class WorkspaceShellRenderTests
{
    private sealed class FakePipelineService : IPipelineService
    {
        public Task<PipelineStartResult> ExplainAsync(string source) => Task.FromResult(new PipelineStartResult { Accepted = true, CorrelationId = "corr" });

        public Task<PipelineStartResult> RunAsync(string source) => throw new NotImplementedException();

        public Task<PipelineStartResult> TraceAsync(string source) => Task.FromResult(new PipelineStartResult { Accepted = true, CorrelationId = "corr" });
    }

    private sealed class FakeFilePickerService : IFilePickerService
    {
        public Task<string?> PickCnlFileAsync() => throw new NotImplementedException();

        public Task<string?> PickFolderAsync() => throw new NotImplementedException();
    }

    private sealed class FakeModalService : IModalService
    {
        public Task ShowBlockedNavigationAsync(string reason) => Task.CompletedTask;

        public Task<bool> ShowUnsavedChangesConfirmationAsync() => Task.FromResult(true);

        public Task ShowFatalErrorAsync(string message) => Task.CompletedTask;
    }

    private static string CreateTempFolderWithFiles(out string llxPath, out string txtPath)
    {
        var root = Path.Combine(Path.GetTempPath(), "llx-shell-render-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        llxPath = Path.Combine(root, "program.llx");
        txtPath = Path.Combine(root, "notes.txt");
        File.WriteAllText(llxPath, "Load the article from \"a.txt\".");
        File.WriteAllText(txtPath, "just some notes");
        File.WriteAllText(Path.Combine(root, "sub", "nested.txt"), "nested");
        return root;
    }

    private static WorkspaceViewModel MakeWorkspace()
    {
        var pipeline = new FakePipelineService();
        var eventStream = new TestDoubles.FakeEventStreamService();
        var lockService = new ExecutionLockService();
        var tabFactory = new TabFactory(pipeline, eventStream, lockService);
        return new WorkspaceViewModel(tabFactory, new FakeFilePickerService(), new FakeModalService(), lockService);
    }

    [AvaloniaFact]
    public void FileTreeView_RendersFolderContentsAndExpandsSubfolder()
    {
        var root = CreateTempFolderWithFiles(out _, out _);
        try
        {
            var workspace = MakeWorkspace();
            workspace.OpenRoot(root);

            var view = new FileTreeView { DataContext = workspace };
            var window = new Window { Content = view, Width = 300, Height = 500 };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var subNode = workspace.FileTree.Nodes.Single(n => n.Name == "sub");
            subNode.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();

            Assert.Single(subNode.Children);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [AvaloniaFact]
    public void TabStrip_RendersOpenTabs_AndSelectTabCommandSwitchesActiveTab()
    {
        var root = CreateTempFolderWithFiles(out var llxPath, out var txtPath);
        try
        {
            var workspace = MakeWorkspace();
            workspace.OpenRoot(root);
            workspace.OpenOrFocusTabCommand.Execute(workspace.FileTree.Nodes.Single(n => n.FullPath == llxPath));
            workspace.OpenOrFocusTabCommand.Execute(workspace.FileTree.Nodes.Single(n => n.FullPath == txtPath));
            var firstTab = workspace.OpenTabs[0];

            var view = new TabStrip { DataContext = workspace };
            var window = new Window { Content = view, Width = 600, Height = 40 };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            workspace.SelectTabCommand.Execute(firstTab);

            Assert.Same(firstTab, workspace.ActiveTab);
            Assert.True(firstTab.IsActive);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [AvaloniaFact]
    public void TabContentHost_ShowsWelcomeState_ThenSwapsToCnlTabView_ThenPlainTextEditor()
    {
        var root = CreateTempFolderWithFiles(out var llxPath, out var txtPath);
        try
        {
            var workspace = MakeWorkspace();
            workspace.OpenRoot(root);

            var view = new TabContentHost { DataContext = workspace };
            var window = new Window { Content = view, Width = 900, Height = 700 };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            workspace.OpenOrFocusTabCommand.Execute(workspace.FileTree.Nodes.Single(n => n.FullPath == llxPath));
            Dispatcher.UIThread.RunJobs();
            Assert.IsType<CnlTabViewModel>(workspace.ActiveTab);

            workspace.OpenOrFocusTabCommand.Execute(workspace.FileTree.Nodes.Single(n => n.FullPath == txtPath));
            Dispatcher.UIThread.RunJobs();
            Assert.IsType<PlainTextTabViewModel>(workspace.ActiveTab);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
