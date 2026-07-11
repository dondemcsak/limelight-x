using LimelightX.UI.Intellisense;
using LimelightX.UI.Services;
using LimelightX.UI.ViewModels.Tabs;
using LimelightX.UI.ViewModels.Workspace;
using Xunit;

namespace LimelightX.UI.Tests.Workspace;

public class TabLifecycleTests
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

        public Task<string?> PickAnyFileAsync() => throw new NotImplementedException();

        public Task<string?> PickSaveFileAsync(string suggestedFileName, string? defaultExtension) => throw new NotImplementedException();
    }

    private sealed class FakeModalService : IModalService
    {
        public bool DiscardOnUnsavedChanges { get; set; } = true;
        public int UnsavedChangesPromptCount { get; private set; }

        public Task ShowBlockedNavigationAsync(string reason) => Task.CompletedTask;

        public Task<bool> ShowUnsavedChangesConfirmationAsync()
        {
            UnsavedChangesPromptCount++;
            return Task.FromResult(DiscardOnUnsavedChanges);
        }

        public Task ShowFatalErrorAsync(string message) => Task.CompletedTask;
    }

    private static string CreateTempFolderWithThreeFiles(out string[] paths)
    {
        var root = Path.Combine(Path.GetTempPath(), "llx-tab-lifecycle-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        paths =
        [
            Path.Combine(root, "a.llx"),
            Path.Combine(root, "b.llx"),
            Path.Combine(root, "c.llx"),
        ];
        foreach (var path in paths)
        {
            File.WriteAllText(path, "Load the article from \"a.txt\".");
        }

        return root;
    }

    private static WorkspaceViewModel MakeWorkspace(FakeModalService? modal = null)
    {
        var pipeline = new FakePipelineService();
        var eventStream = new TestDoubles.FakeEventStreamService();
        var lockService = new ExecutionLockService();
        var tabFactory = new TabFactory(pipeline, eventStream, lockService, new TestDoubles.FakeCompletionService(), new TestDoubles.FakeDiagnosticService(), new TestDoubles.FakeHoverService(), new TestDoubles.FakeFoldingService(), new TestDoubles.FakeStructuralSelectionService(), new TestDoubles.FakeOutlineService(), new TestDoubles.FakeAutoPairService(), new TestDoubles.FakeNavigationService(), () => new TestDoubles.FakeParserHost());
        return new WorkspaceViewModel(tabFactory, new FakeFilePickerService(), modal ?? new FakeModalService(), lockService);
    }

    [Fact]
    public async Task CloseTabCommand_CleanTab_RemovesWithoutPrompting()
    {
        var root = CreateTempFolderWithThreeFiles(out var paths);
        try
        {
            var modal = new FakeModalService();
            var workspace = MakeWorkspace(modal);
            workspace.OpenRoot(root);
            var node = workspace.FileTree.Nodes.Single(n => n.FullPath == paths[0]);
            workspace.OpenOrFocusTabCommand.Execute(node);
            var tab = workspace.OpenTabs[0];

            await workspace.CloseTabCommand.ExecuteAsync(tab);

            Assert.Empty(workspace.OpenTabs);
            Assert.Equal(0, modal.UnsavedChangesPromptCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CloseTabCommand_DirtyTab_PromptsAndKeepsTabOpenIfUserStays()
    {
        var root = CreateTempFolderWithThreeFiles(out var paths);
        try
        {
            var modal = new FakeModalService { DiscardOnUnsavedChanges = false };
            var workspace = MakeWorkspace(modal);
            workspace.OpenRoot(root);
            var node = workspace.FileTree.Nodes.Single(n => n.FullPath == paths[0]);
            workspace.OpenOrFocusTabCommand.Execute(node);
            var tab = (CnlTabViewModel)workspace.OpenTabs[0];
            tab.Editor.Text = "Load the article from \"b.txt\".";
            Assert.True(tab.IsDirty);

            await workspace.CloseTabCommand.ExecuteAsync(tab);

            Assert.Single(workspace.OpenTabs);
            Assert.Equal(1, modal.UnsavedChangesPromptCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CloseTabCommand_DirtyTab_RemovesWhenUserDiscards()
    {
        var root = CreateTempFolderWithThreeFiles(out var paths);
        try
        {
            var modal = new FakeModalService { DiscardOnUnsavedChanges = true };
            var workspace = MakeWorkspace(modal);
            workspace.OpenRoot(root);
            var node = workspace.FileTree.Nodes.Single(n => n.FullPath == paths[0]);
            workspace.OpenOrFocusTabCommand.Execute(node);
            var tab = (CnlTabViewModel)workspace.OpenTabs[0];
            tab.Editor.Text = "Load the article from \"b.txt\".";

            await workspace.CloseTabCommand.ExecuteAsync(tab);

            Assert.Empty(workspace.OpenTabs);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void NextTabCommand_And_PreviousTabCommand_CycleThroughOpenTabsInOrder()
    {
        var root = CreateTempFolderWithThreeFiles(out var paths);
        try
        {
            var workspace = MakeWorkspace();
            workspace.OpenRoot(root);
            foreach (var path in paths)
            {
                var node = workspace.FileTree.Nodes.Single(n => n.FullPath == path);
                workspace.OpenOrFocusTabCommand.Execute(node);
            }

            // Opened a, b, c in order - c is active after the loop above.
            Assert.Same(workspace.OpenTabs[2], workspace.ActiveTab);

            workspace.NextTabCommand.Execute(null);
            Assert.Same(workspace.OpenTabs[0], workspace.ActiveTab);

            workspace.PreviousTabCommand.Execute(null);
            Assert.Same(workspace.OpenTabs[2], workspace.ActiveTab);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CloseActiveTabCommand_ClosesCurrentlyActiveTab()
    {
        var root = CreateTempFolderWithThreeFiles(out var paths);
        try
        {
            var workspace = MakeWorkspace();
            workspace.OpenRoot(root);
            var node = workspace.FileTree.Nodes.Single(n => n.FullPath == paths[0]);
            workspace.OpenOrFocusTabCommand.Execute(node);

            await workspace.CloseActiveTabCommand.ExecuteAsync(null);

            Assert.Empty(workspace.OpenTabs);
            Assert.Null(workspace.ActiveTab);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CloseTabCommand_ActiveTab_ActivatesAnotherRemainingTab()
    {
        var root = CreateTempFolderWithThreeFiles(out var paths);
        try
        {
            var workspace = MakeWorkspace();
            workspace.OpenRoot(root);
            foreach (var path in paths.Take(2))
            {
                var node = workspace.FileTree.Nodes.Single(n => n.FullPath == path);
                workspace.OpenOrFocusTabCommand.Execute(node);
            }

            var activeTab = workspace.ActiveTab!;
            await workspace.CloseTabCommand.ExecuteAsync(activeTab);

            Assert.Single(workspace.OpenTabs);
            Assert.NotSame(activeTab, workspace.ActiveTab);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
