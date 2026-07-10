using LimelightX.UI.Intellisense;
using LimelightX.UI.Services;
using LimelightX.UI.Tests.TestDoubles;
using LimelightX.UI.ViewModels.Errors;
using LimelightX.UI.ViewModels.Tabs;
using LimelightX.UI.ViewModels.Workspace;
using Xunit;

namespace LimelightX.UI.Tests.Execution;

/// <summary>
/// ui-error-handling.md §9.3: "Switching Tabs Does Not Clear Another Tab's
/// Banner" - an explicit reversal of the old single-page-model rule where
/// navigating away from the Execution Page cleared the banner. Each tab's
/// ErrorBanner is independent (owned by that tab's own
/// PipelineExecutionViewModel), so switching ActiveTab must never touch it.
/// </summary>
public class ErrorPersistenceAcrossTabSwitchTests
{
    private sealed class FakePipelineService : IPipelineService
    {
        public Task<PipelineStartResult> ExplainAsync(string source) => Task.FromResult(new PipelineStartResult { Accepted = true, CorrelationId = "corr-explain" });

        public Task<PipelineStartResult> RunAsync(string source) => throw new NotImplementedException();

        public Task<PipelineStartResult> TraceAsync(string source) => Task.FromResult(new PipelineStartResult { Accepted = true, CorrelationId = "corr-run" });
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

    private static string CreateTempFolderWithTwoFiles(out string[] paths)
    {
        var root = Path.Combine(Path.GetTempPath(), "llx-error-persistence-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        paths = [Path.Combine(root, "a.llx"), Path.Combine(root, "b.llx")];
        foreach (var path in paths)
        {
            File.WriteAllText(path, "Load the article from \"a.txt\".");
        }

        return root;
    }

    [Fact]
    public async Task SwitchingAwayAndBack_DoesNotClearTheFailingTabsBanner()
    {
        var root = CreateTempFolderWithTwoFiles(out var paths);
        try
        {
            var lockService = new ExecutionLockService();
            var eventStreamA = new FakeEventStreamService();
            var tabFactory = new TabFactory(new FakePipelineService(), eventStreamA, lockService, new CompletionService(), new DiagnosticService(), new HoverService(), new FoldingService(new TestDoubles.FakeQueryRunner()), new TestDoubles.FakeStructuralSelectionService());
            var workspace = new WorkspaceViewModel(tabFactory, new FakeFilePickerService(), new FakeModalService(), lockService);
            workspace.OpenRoot(root);

            workspace.OpenOrFocusTabCommand.Execute(workspace.FileTree.Nodes.Single(n => n.FullPath == paths[0]));
            var tabA = (CnlTabViewModel)workspace.OpenTabs[0];
            workspace.OpenOrFocusTabCommand.Execute(workspace.FileTree.Nodes.Single(n => n.FullPath == paths[1]));
            var tabB = workspace.OpenTabs[1];

            // Establish tabA's active correlation_id before raising events -
            // PipelineExecutionViewModel ignores events for a correlation_id
            // it never started (ui-data-contracts.md §10).
            await tabA.Editor.RunCommand.ExecuteAsync(null);
            eventStreamA.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-run"));
            eventStreamA.Raise(FakeEventStreamService.MakeEvent(
                "pipeline_failed",
                "corr-run",
                success: false,
                errors: [new UiError { Code = "ERR_CNL_NORMALIZE", Message = "bad pronoun", Severity = ErrorSeverity.Error, Category = ErrorCategory.Pipeline }]));

            Assert.True(tabA.PipelineExecution.ErrorBanner.IsVisible);

            // Switch to B, then back to A.
            workspace.ActiveTab = tabB;
            workspace.ActiveTab = tabA;

            Assert.True(tabA.PipelineExecution.ErrorBanner.IsVisible);
            Assert.Single(tabA.PipelineExecution.ErrorBanner.Errors);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SwitchingToATabWithAFailure_NeverShowsItOnAnotherTab()
    {
        var root = CreateTempFolderWithTwoFiles(out var paths);
        try
        {
            var lockService = new ExecutionLockService();
            var eventStream = new FakeEventStreamService();
            var tabFactory = new TabFactory(new FakePipelineService(), eventStream, lockService, new CompletionService(), new DiagnosticService(), new HoverService(), new FoldingService(new TestDoubles.FakeQueryRunner()), new TestDoubles.FakeStructuralSelectionService());
            var workspace = new WorkspaceViewModel(tabFactory, new FakeFilePickerService(), new FakeModalService(), lockService);
            workspace.OpenRoot(root);

            workspace.OpenOrFocusTabCommand.Execute(workspace.FileTree.Nodes.Single(n => n.FullPath == paths[0]));
            var tabA = (CnlTabViewModel)workspace.OpenTabs[0];
            workspace.OpenOrFocusTabCommand.Execute(workspace.FileTree.Nodes.Single(n => n.FullPath == paths[1]));
            var tabB = (CnlTabViewModel)workspace.OpenTabs[1];

            await tabA.Editor.RunCommand.ExecuteAsync(null);
            eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-run"));
            eventStream.Raise(FakeEventStreamService.MakeEvent(
                "pipeline_failed",
                "corr-run",
                success: false,
                errors: [new UiError { Code = "ERR_CNL_NORMALIZE", Message = "bad pronoun", Severity = ErrorSeverity.Error, Category = ErrorCategory.Pipeline }]));

            Assert.True(tabA.PipelineExecution.ErrorBanner.IsVisible);
            Assert.False(tabB.PipelineExecution.ErrorBanner.IsVisible);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
