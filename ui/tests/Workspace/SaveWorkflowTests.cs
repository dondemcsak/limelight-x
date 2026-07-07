using LimelightX.UI.Services;
using LimelightX.UI.ViewModels.Tabs;
using LimelightX.UI.ViewModels.Workspace;
using Xunit;

namespace LimelightX.UI.Tests.Workspace;

/// <summary>
/// New LLX/TXT File, Open File, and Save/Save As/Save All (ui-viewmodels.md
/// §3) - all net-new WorkspaceViewModel commands, no prior file-write code
/// path existed anywhere in /ui before this.
/// </summary>
public class SaveWorkflowTests
{
    private sealed class FakePipelineService : IPipelineService
    {
        public Task<PipelineStartResult> ExplainAsync(string source) => Task.FromResult(new PipelineStartResult { Accepted = true, CorrelationId = "corr" });

        public Task<PipelineStartResult> RunAsync(string source) => throw new NotImplementedException();

        public Task<PipelineStartResult> TraceAsync(string source) => Task.FromResult(new PipelineStartResult { Accepted = true, CorrelationId = "corr" });
    }

    private sealed class FakeFilePickerService : IFilePickerService
    {
        public string? AnyFilePath { get; set; }

        public string? SaveFilePath { get; set; }

        public Task<string?> PickCnlFileAsync() => throw new NotImplementedException();

        public Task<string?> PickFolderAsync() => throw new NotImplementedException();

        public Task<string?> PickAnyFileAsync() => Task.FromResult(AnyFilePath);

        public Task<string?> PickSaveFileAsync(string suggestedFileName, string? defaultExtension) => Task.FromResult(SaveFilePath);
    }

    private sealed class FakeModalService : IModalService
    {
        public Task<bool> ShowUnsavedChangesConfirmationAsync() => Task.FromResult(true);

        public Task ShowFatalErrorAsync(string message) => Task.CompletedTask;
    }

    private static (WorkspaceViewModel Workspace, FakeFilePickerService FilePicker) MakeWorkspace()
    {
        var pipeline = new FakePipelineService();
        var eventStream = new TestDoubles.FakeEventStreamService();
        var lockService = new ExecutionLockService();
        var tabFactory = new TabFactory(pipeline, eventStream, lockService);
        var filePicker = new FakeFilePickerService();
        var workspace = new WorkspaceViewModel(tabFactory, filePicker, new FakeModalService(), lockService);
        return (workspace, filePicker);
    }

    [Fact]
    public void NewLlxFileCommand_CreatesUntitledCnlTab_WithUntitled1Header_NotDirty()
    {
        var (workspace, _) = MakeWorkspace();

        workspace.NewLlxFileCommand.Execute(null);

        var tab = Assert.Single(workspace.OpenTabs);
        Assert.IsType<CnlTabViewModel>(tab);
        Assert.Equal("Untitled-1", tab.Header);
        Assert.True(tab.IsUntitled);
        Assert.Null(tab.FilePath);
        Assert.False(tab.IsDirty);
        Assert.Same(tab, workspace.ActiveTab);
    }

    [Fact]
    public void NewTxtFileCommand_AfterNewLlxFile_ContinuesSharedCounter()
    {
        var (workspace, _) = MakeWorkspace();

        workspace.NewLlxFileCommand.Execute(null);
        workspace.NewTxtFileCommand.Execute(null);

        Assert.Equal(2, workspace.OpenTabs.Count);
        var second = workspace.OpenTabs[1];
        Assert.IsType<PlainTextTabViewModel>(second);
        Assert.Equal("Untitled-2", second.Header);
    }

    [Fact]
    public async Task OpenFileCommand_LlxPath_CreatesCnlTab()
    {
        var (workspace, filePicker) = MakeWorkspace();
        var root = CreateTempFolder();
        try
        {
            var llxPath = Path.Combine(root, "program.llx");
            File.WriteAllText(llxPath, "Load the article from \"a.txt\".");
            filePicker.AnyFilePath = llxPath;

            await workspace.OpenFileCommand.ExecuteAsync(null);

            var tab = Assert.Single(workspace.OpenTabs);
            Assert.IsType<CnlTabViewModel>(tab);
            Assert.Equal(llxPath, tab.FilePath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OpenFileCommand_NonLlxPath_CreatesPlainTextTab()
    {
        var (workspace, filePicker) = MakeWorkspace();
        var root = CreateTempFolder();
        try
        {
            var txtPath = Path.Combine(root, "notes.txt");
            File.WriteAllText(txtPath, "notes");
            filePicker.AnyFilePath = txtPath;

            await workspace.OpenFileCommand.ExecuteAsync(null);

            var tab = Assert.Single(workspace.OpenTabs);
            Assert.IsType<PlainTextTabViewModel>(tab);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OpenFileCommand_UserCancels_NoTabCreated()
    {
        var (workspace, filePicker) = MakeWorkspace();
        filePicker.AnyFilePath = null;

        await workspace.OpenFileCommand.ExecuteAsync(null);

        Assert.Empty(workspace.OpenTabs);
    }

    [Fact]
    public void SaveCommand_CanExecute_FalseWithNoActiveTab_TrueOnceTabOpened()
    {
        var (workspace, _) = MakeWorkspace();

        Assert.False(workspace.SaveCommand.CanExecute(null));

        workspace.NewLlxFileCommand.Execute(null);

        Assert.True(workspace.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task SaveCommand_UntitledTab_PromptsForLocation_WritesFile_ClearsIsUntitled_SetsFilePath_ClearsIsDirty()
    {
        var (workspace, filePicker) = MakeWorkspace();
        var root = CreateTempFolder();
        try
        {
            workspace.NewLlxFileCommand.Execute(null);
            var tab = workspace.ActiveTab!;
            var cnl = (CnlTabViewModel)tab;
            cnl.Editor.Text = "Load the article from \"a.txt\".";
            var savePath = Path.Combine(root, "Untitled-1.llx");
            filePicker.SaveFilePath = savePath;

            await workspace.SaveCommand.ExecuteAsync(null);

            Assert.False(tab.IsUntitled);
            Assert.Equal(savePath, tab.FilePath);
            Assert.False(tab.IsDirty);
            Assert.Equal("Load the article from \"a.txt\".", File.ReadAllText(savePath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveCommand_ExistingPathTab_WritesDirectly_DoesNotPrompt()
    {
        var (workspace, filePicker) = MakeWorkspace();
        var root = CreateTempFolder();
        try
        {
            var llxPath = Path.Combine(root, "program.llx");
            File.WriteAllText(llxPath, "original");
            workspace.OpenRoot(root);
            workspace.OpenOrFocusTabCommand.Execute(workspace.FileTree.Nodes.Single(n => n.FullPath == llxPath));
            var tab = workspace.ActiveTab!;
            var cnl = (CnlTabViewModel)tab;
            cnl.Editor.Text = "changed";
            filePicker.SaveFilePath = null; // proves no prompt is needed - a prompt would return null and fail the save

            await workspace.SaveCommand.ExecuteAsync(null);

            Assert.False(tab.IsDirty);
            Assert.Equal("changed", File.ReadAllText(llxPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsCommand_AlwaysPrompts_EvenWhenTabAlreadyHasPath()
    {
        var (workspace, filePicker) = MakeWorkspace();
        var root = CreateTempFolder();
        try
        {
            var llxPath = Path.Combine(root, "program.llx");
            File.WriteAllText(llxPath, "original");
            workspace.OpenRoot(root);
            workspace.OpenOrFocusTabCommand.Execute(workspace.FileTree.Nodes.Single(n => n.FullPath == llxPath));
            var tab = workspace.ActiveTab!;
            var redirectPath = Path.Combine(root, "renamed.llx");
            filePicker.SaveFilePath = redirectPath;

            await workspace.SaveAsCommand.ExecuteAsync(null);

            Assert.Equal(redirectPath, tab.FilePath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveAllCommand_CanExecute_FalseWithNoDirtyTabs_TrueOnceATabIsDirty()
    {
        var (workspace, _) = MakeWorkspace();
        workspace.NewLlxFileCommand.Execute(null);

        Assert.False(workspace.SaveAllCommand.CanExecute(null));

        var cnl = (CnlTabViewModel)workspace.ActiveTab!;
        cnl.Editor.Text = "not empty anymore";

        Assert.True(workspace.SaveAllCommand.CanExecute(null));
    }

    [Fact]
    public async Task SaveAllCommand_SkipsCancelledTab_ContinuesSavingRemainingDirtyTabs()
    {
        var (workspace, filePicker) = MakeWorkspace();
        var root = CreateTempFolder();
        try
        {
            var llxPath = Path.Combine(root, "program.llx");
            File.WriteAllText(llxPath, "original");
            workspace.OpenRoot(root);
            workspace.OpenOrFocusTabCommand.Execute(workspace.FileTree.Nodes.Single(n => n.FullPath == llxPath));
            var existingTab = (CnlTabViewModel)workspace.ActiveTab!;
            existingTab.Editor.Text = "changed";

            workspace.NewTxtFileCommand.Execute(null);
            var untitledTab = workspace.ActiveTab!;
            var plainText = (PlainTextTabViewModel)untitledTab;
            plainText.Editor.Text = "untitled content";

            filePicker.SaveFilePath = null; // the untitled tab's prompt is cancelled

            await workspace.SaveAllCommand.ExecuteAsync(null);

            Assert.False(existingTab.IsDirty);
            Assert.Equal("changed", File.ReadAllText(llxPath));
            Assert.True(untitledTab.IsDirty);
            Assert.True(untitledTab.IsUntitled);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveCommand_WriteFailure_LeavesTabDirtyAndUntitled_AddsErrorToTabErrorBanner()
    {
        var (workspace, filePicker) = MakeWorkspace();
        var root = CreateTempFolder();
        try
        {
            workspace.NewLlxFileCommand.Execute(null);
            var tab = workspace.ActiveTab!;
            var cnl = (CnlTabViewModel)tab;
            cnl.Editor.Text = "content";

            // A path that is itself an existing directory - File.WriteAllTextAsync
            // throws UnauthorizedAccessException/IOException for this.
            filePicker.SaveFilePath = root;

            await workspace.SaveCommand.ExecuteAsync(null);

            Assert.True(tab.IsUntitled);
            Assert.True(tab.IsDirty);
            Assert.True(tab.ErrorBanner.IsVisible);
            Assert.NotEmpty(tab.ErrorBanner.Errors);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "llx-save-workflow-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
