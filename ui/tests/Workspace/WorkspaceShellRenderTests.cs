using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LimelightX.UI.Components;
using LimelightX.UI.Intellisense;
using LimelightX.UI.Services;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels;
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

        public Task<string?> PickAnyFileAsync() => throw new NotImplementedException();

        public Task<string?> PickSaveFileAsync(string suggestedFileName, string? defaultExtension) => throw new NotImplementedException();
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
        var tabFactory = new TabFactory(pipeline, eventStream, lockService, new TestDoubles.FakeCompletionService(), new TestDoubles.FakeDiagnosticService(), new TestDoubles.FakeHoverService(), new TestDoubles.FakeFoldingService(), new TestDoubles.FakeStructuralSelectionService(), new TestDoubles.FakeOutlineService(), () => new TestDoubles.FakeParserHost());
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

    [AvaloniaFact]
    public async Task CnlTabView_ProgressIndicator_ShowsWhileRunningAndHidesOnCompletion()
    {
        var root = CreateTempFolderWithFiles(out var llxPath, out _);
        try
        {
            var eventStream = new TestDoubles.FakeEventStreamService();
            var lockService = new ExecutionLockService();
            var tabFactory = new TabFactory(new FakePipelineService(), eventStream, lockService, new TestDoubles.FakeCompletionService(), new TestDoubles.FakeDiagnosticService(), new TestDoubles.FakeHoverService(), new TestDoubles.FakeFoldingService(), new TestDoubles.FakeStructuralSelectionService(), new TestDoubles.FakeOutlineService(), () => new TestDoubles.FakeParserHost());
            var workspace = new WorkspaceViewModel(tabFactory, new FakeFilePickerService(), new FakeModalService(), lockService);
            workspace.OpenRoot(root);
            workspace.OpenOrFocusTabCommand.Execute(workspace.FileTree.Nodes.Single(n => n.FullPath == llxPath));
            var tab = (CnlTabViewModel)workspace.ActiveTab!;

            var view = new CnlTabView { DataContext = tab };
            var window = new Window { Content = view, Width = 900, Height = 700 };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var indicator = view.FindControl<LoadingIndicator>("ProgressIndicator")!;
            Assert.False(indicator.IsLoading);

            await tab.Editor.RunCommand.ExecuteAsync(null);
            eventStream.Raise(TestDoubles.FakeEventStreamService.MakeEvent("pipeline_started", "corr"));
            Dispatcher.UIThread.RunJobs();
            Assert.True(indicator.IsLoading);

            eventStream.Raise(TestDoubles.FakeEventStreamService.MakeEvent(
                "final_result_ready",
                "corr",
                new RunData { FinalResult = new FinalResult { Text = "done", ContentType = ResultContentType.Plain } }));
            Dispatcher.UIThread.RunJobs();
            Assert.False(indicator.IsLoading);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [AvaloniaFact]
    public void MenuBar_RendersFileAndHelpMenus()
    {
        var workspace = MakeWorkspace();

        var view = new MenuBar { DataContext = workspace };
        var window = new Window { Content = view, Width = 400, Height = 40 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var menu = view.FindControl<Menu>("RootMenu")!;

        Assert.Equal(2, menu.Items.Count);
        var headers = menu.Items.OfType<MenuItem>().Select(item => item.Header?.ToString() ?? string.Empty).ToList();
        Assert.Contains(headers, h => h.Contains("File"));
        Assert.Contains(headers, h => h.Contains("Help"));
    }

    [AvaloniaFact]
    public void MenuBar_SaveCommand_DisabledWithNoActiveTab_EnabledAfterOpeningTab()
    {
        var root = CreateTempFolderWithFiles(out var llxPath, out _);
        try
        {
            var workspace = MakeWorkspace();

            var view = new MenuBar { DataContext = workspace };
            var window = new Window { Content = view, Width = 400, Height = 40 };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspace.SaveCommand.CanExecute(null));

            workspace.OpenRoot(root);
            workspace.OpenOrFocusTabCommand.Execute(workspace.FileTree.Nodes.Single(n => n.FullPath == llxPath));

            Assert.True(workspace.SaveCommand.CanExecute(null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [AvaloniaFact]
    public void AboutModalView_RendersProjectInfo()
    {
        var about = new AboutViewModel();

        var view = new AboutModalView { DataContext = about };
        var window = new Window { Content = view, Width = 420, Height = 260 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var appNameText = view.FindControl<TextBlock>("AppNameText")!;
        var descriptionText = view.FindControl<TextBlock>("DescriptionText")!;
        var versionText = view.FindControl<TextBlock>("VersionText")!;

        Assert.Equal(about.AppName, appNameText.Text);
        Assert.False(string.IsNullOrWhiteSpace(descriptionText.Text));
        Assert.StartsWith("Version ", versionText.Text);
    }
}
