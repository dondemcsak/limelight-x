using LimelightX.UI.Services;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.Tests.TestDoubles;
using LimelightX.UI.ViewModels.Tabs;
using LimelightX.UI.ViewModels.Workspace;
using Xunit;

namespace LimelightX.UI.Tests.Execution;

/// <summary>
/// ui-testing.md §4.3/§7.2: starting Run in one tab must disable Run/Explain
/// on every other open tab and the Settings gear, while tab switch/open/close
/// and folder browsing remain unaffected app-wide.
/// </summary>
public class CrossTabExecutionLockTests
{
    private sealed class FakePipelineService : IPipelineService
    {
        public Task<PipelineStartResult> ExplainAsync(string source) => Task.FromResult(new PipelineStartResult { Accepted = true, CorrelationId = Guid.NewGuid().ToString() });

        public Task<PipelineStartResult> RunAsync(string source) => throw new NotImplementedException();

        public Task<PipelineStartResult> TraceAsync(string source) => Task.FromResult(new PipelineStartResult { Accepted = true, CorrelationId = "corr-run" });
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

    private static string CreateTempFolderWithTwoFiles(out string[] paths)
    {
        var root = Path.Combine(Path.GetTempPath(), "llx-cross-tab-lock-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        paths = [Path.Combine(root, "a.llx"), Path.Combine(root, "b.llx")];
        foreach (var path in paths)
        {
            File.WriteAllText(path, "Load the article from \"a.txt\".");
        }

        return root;
    }

    [Fact]
    public async Task StartingRunInTabA_DisablesRunExplainOnTabB_AndSettingsGear()
    {
        var root = CreateTempFolderWithTwoFiles(out var paths);
        try
        {
            var lockService = new ExecutionLockService();
            var eventStream = new FakeEventStreamService();
            var tabFactory = new TabFactory(new FakePipelineService(), eventStream, lockService);
            var workspace = new WorkspaceViewModel(tabFactory, new FakeFilePickerService(), new FakeModalService(), lockService);
            workspace.OpenRoot(root);

            var nodeA = workspace.FileTree.Nodes.Single(n => n.FullPath == paths[0]);
            var nodeB = workspace.FileTree.Nodes.Single(n => n.FullPath == paths[1]);
            workspace.OpenOrFocusTabCommand.Execute(nodeA);
            var tabA = (CnlTabViewModel)workspace.OpenTabs[0];
            workspace.OpenOrFocusTabCommand.Execute(nodeB);
            var tabB = (CnlTabViewModel)workspace.OpenTabs[1];

            Assert.True(tabB.Editor.RunCommand.CanExecute(null));
            Assert.True(workspace.OpenSettingsCommand.CanExecute(null));

            await tabA.Editor.RunCommand.ExecuteAsync(null);
            // FakePipelineService's TraceAsync only returns an ack - the real
            // pipeline_started event is what flips IsAnyExecutionRunning
            // (ui-viewmodels.md §7); simulate it the same way EventStreamService would.
            eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-run"));

            Assert.False(tabB.Editor.RunCommand.CanExecute(null));
            Assert.False(tabB.Editor.ExplainCommand.CanExecute(null));
            Assert.False(workspace.OpenSettingsCommand.CanExecute(null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartingRunInTabA_DoesNotBlockSwitchingOpeningOrClosingTabB()
    {
        var root = CreateTempFolderWithTwoFiles(out var paths);
        try
        {
            var lockService = new ExecutionLockService();
            var eventStream = new FakeEventStreamService();
            var tabFactory = new TabFactory(new FakePipelineService(), eventStream, lockService);
            var workspace = new WorkspaceViewModel(tabFactory, new FakeFilePickerService(), new FakeModalService(), lockService);
            workspace.OpenRoot(root);

            var nodeA = workspace.FileTree.Nodes.Single(n => n.FullPath == paths[0]);
            var nodeB = workspace.FileTree.Nodes.Single(n => n.FullPath == paths[1]);
            workspace.OpenOrFocusTabCommand.Execute(nodeA);
            var tabA = (CnlTabViewModel)workspace.OpenTabs[0];
            workspace.OpenOrFocusTabCommand.Execute(nodeB);
            var tabB = workspace.OpenTabs[1];

            await tabA.Editor.RunCommand.ExecuteAsync(null);
            eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-run"));
            Assert.True(lockService.IsAnyExecutionRunning);

            // Switching to, and closing, tab B must both succeed while locked.
            workspace.ActiveTab = tabB;
            Assert.Same(tabB, workspace.ActiveTab);

            await workspace.CloseTabCommand.ExecuteAsync(tabB);
            Assert.Single(workspace.OpenTabs);

            // The lock is still held by tab A throughout.
            Assert.True(lockService.IsAnyExecutionRunning);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PipelineCompletes_ReenablesRunExplainAndSettingsGearEverywhere()
    {
        var root = CreateTempFolderWithTwoFiles(out var paths);
        try
        {
            var lockService = new ExecutionLockService();
            var eventStream = new FakeEventStreamService();
            var tabFactory = new TabFactory(new FakePipelineService(), eventStream, lockService);
            var workspace = new WorkspaceViewModel(tabFactory, new FakeFilePickerService(), new FakeModalService(), lockService);
            workspace.OpenRoot(root);

            var nodeA = workspace.FileTree.Nodes.Single(n => n.FullPath == paths[0]);
            var nodeB = workspace.FileTree.Nodes.Single(n => n.FullPath == paths[1]);
            workspace.OpenOrFocusTabCommand.Execute(nodeA);
            var tabA = (CnlTabViewModel)workspace.OpenTabs[0];
            workspace.OpenOrFocusTabCommand.Execute(nodeB);
            var tabB = (CnlTabViewModel)workspace.OpenTabs[1];

            await tabA.Editor.RunCommand.ExecuteAsync(null);
            eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-run"));
            Assert.False(tabB.Editor.RunCommand.CanExecute(null));

            eventStream.Raise(FakeEventStreamService.MakeEvent(
                "final_result_ready",
                "corr-run",
                new RunData { FinalResult = new FinalResult { Text = "done", ContentType = ResultContentType.Plain } }));

            Assert.True(tabB.Editor.RunCommand.CanExecute(null));
            Assert.True(tabB.Editor.ExplainCommand.CanExecute(null));
            Assert.True(workspace.OpenSettingsCommand.CanExecute(null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
