using LimelightX.UI.Services;
using LimelightX.UI.ViewModels;
using LimelightX.UI.ViewModels.Tabs;
using LimelightX.UI.ViewModels.Workspace;
using Xunit;

namespace LimelightX.UI.Tests.Workspace;

public class WorkspaceViewModelTests
{
    private sealed class FakePipelineService : IPipelineService
    {
        public Task<PipelineStartResult> ExplainAsync(string source) => Task.FromResult(new PipelineStartResult { Accepted = true, CorrelationId = "corr" });

        public Task<PipelineStartResult> RunAsync(string source) => throw new NotImplementedException();

        public Task<PipelineStartResult> TraceAsync(string source) => Task.FromResult(new PipelineStartResult { Accepted = true, CorrelationId = "corr" });
    }

    private sealed class FakeFilePickerService(string? folderPath) : IFilePickerService
    {
        public Task<string?> PickCnlFileAsync() => throw new NotImplementedException();

        public Task<string?> PickFolderAsync() => Task.FromResult(folderPath);

        public Task<string?> PickAnyFileAsync() => throw new NotImplementedException();

        public Task<string?> PickSaveFileAsync(string suggestedFileName, string? defaultExtension) => throw new NotImplementedException();
    }

    private sealed class FakeModalService : IModalService
    {
        public bool DiscardOnUnsavedChanges { get; set; } = true;

        public Task ShowBlockedNavigationAsync(string reason) => Task.CompletedTask;

        public Task<bool> ShowUnsavedChangesConfirmationAsync() => Task.FromResult(DiscardOnUnsavedChanges);

        public Task ShowFatalErrorAsync(string message) => Task.CompletedTask;
    }

    private static string CreateTempFolderWithFiles(out string llxPath, out string txtPath)
    {
        var root = Path.Combine(Path.GetTempPath(), "llx-workspace-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        llxPath = Path.Combine(root, "program.llx");
        txtPath = Path.Combine(root, "notes.txt");
        File.WriteAllText(llxPath, "Load the article from \"a.txt\".");
        File.WriteAllText(txtPath, "just some notes");
        return root;
    }

    private static WorkspaceViewModel MakeWorkspace(string? folderToPick = null, IModalService? modal = null, IExecutionLockService? executionLock = null)
    {
        var pipeline = new FakePipelineService();
        var eventStream = new TestDoubles.FakeEventStreamService();
        var lockService = executionLock ?? new ExecutionLockService();
        var tabFactory = new TabFactory(pipeline, eventStream, lockService);
        return new WorkspaceViewModel(tabFactory, new FakeFilePickerService(folderToPick), modal ?? new FakeModalService(), lockService);
    }

    [Fact]
    public async Task OpenFolderCommand_PickedPath_OpensRootAndRaisesFolderOpened()
    {
        var root = CreateTempFolderWithFiles(out _, out _);
        try
        {
            var workspace = MakeWorkspace(folderToPick: root);
            string? raisedPath = null;
            workspace.FolderOpened += p => raisedPath = p;

            await workspace.OpenFolderCommand.ExecuteAsync(null);

            Assert.Equal(root, workspace.RootFolderPath);
            Assert.Equal(root, raisedPath);
            Assert.Equal(2, workspace.FileTree.Nodes.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void OpenOrFocusTab_LlxFile_CreatesCnlTabViewModel()
    {
        var root = CreateTempFolderWithFiles(out var llxPath, out _);
        try
        {
            var workspace = MakeWorkspace();
            workspace.OpenRoot(root);
            var node = workspace.FileTree.Nodes.Single(n => n.FullPath == llxPath);

            workspace.OpenOrFocusTabCommand.Execute(node);

            var tab = Assert.Single(workspace.OpenTabs);
            Assert.IsType<CnlTabViewModel>(tab);
            Assert.Equal(llxPath, tab.FilePath);
            Assert.Same(tab, workspace.ActiveTab);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void OpenOrFocusTab_NonLlxFile_CreatesPlainTextTabViewModel()
    {
        var root = CreateTempFolderWithFiles(out _, out var txtPath);
        try
        {
            var workspace = MakeWorkspace();
            workspace.OpenRoot(root);
            var node = workspace.FileTree.Nodes.Single(n => n.FullPath == txtPath);

            workspace.OpenOrFocusTabCommand.Execute(node);

            var tab = Assert.Single(workspace.OpenTabs);
            Assert.IsType<PlainTextTabViewModel>(tab);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void OpenOrFocusTab_AlreadyOpenFile_FocusesInsteadOfDuplicating()
    {
        var root = CreateTempFolderWithFiles(out var llxPath, out _);
        try
        {
            var workspace = MakeWorkspace();
            workspace.OpenRoot(root);
            var node = workspace.FileTree.Nodes.Single(n => n.FullPath == llxPath);

            workspace.OpenOrFocusTabCommand.Execute(node);
            var firstTab = workspace.ActiveTab;
            workspace.ActiveTab = null;
            workspace.OpenOrFocusTabCommand.Execute(node);

            Assert.Single(workspace.OpenTabs);
            Assert.Same(firstTab, workspace.ActiveTab);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void OpenOrFocusTab_NewTab_RaisesTabOpened_RefocusDoesNot()
    {
        var root = CreateTempFolderWithFiles(out var llxPath, out _);
        try
        {
            var workspace = MakeWorkspace();
            workspace.OpenRoot(root);
            var node = workspace.FileTree.Nodes.Single(n => n.FullPath == llxPath);
            var openedCount = 0;
            workspace.TabOpened += _ => openedCount++;

            workspace.OpenOrFocusTabCommand.Execute(node);
            workspace.OpenOrFocusTabCommand.Execute(node);

            Assert.Equal(1, openedCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void OpenSettingsCommand_BlockedWhileAnyExecutionRunning()
    {
        var executionLock = new ExecutionLockService();
        var workspace = MakeWorkspace(executionLock: executionLock);

        Assert.True(workspace.OpenSettingsCommand.CanExecute(null));

        executionLock.TryAcquire(new object());

        Assert.False(workspace.OpenSettingsCommand.CanExecute(null));
    }

    [Fact]
    public void OpenSettingsCommand_SetsIsSettingsOpen_AndCloseSettingsClearsIt()
    {
        var workspace = MakeWorkspace();

        workspace.OpenSettingsCommand.Execute(null);
        Assert.True(workspace.IsSettingsOpen);

        workspace.CloseSettingsCommand.Execute(null);
        Assert.False(workspace.IsSettingsOpen);
    }

    [Fact]
    public void OpenAboutCommand_SetsIsAboutOpen_AndCloseAboutClearsIt()
    {
        var workspace = MakeWorkspace();

        workspace.OpenAboutCommand.Execute(null);
        Assert.True(workspace.IsAboutOpen);

        workspace.CloseAboutCommand.Execute(null);
        Assert.False(workspace.IsAboutOpen);
    }

    [Fact]
    public void OpenAboutCommand_NeverBlockedByExecutionLock()
    {
        var executionLock = new ExecutionLockService();
        var workspace = MakeWorkspace(executionLock: executionLock);

        Assert.True(workspace.OpenAboutCommand.CanExecute(null));

        executionLock.TryAcquire(new object());

        Assert.True(workspace.OpenAboutCommand.CanExecute(null));
    }
}
