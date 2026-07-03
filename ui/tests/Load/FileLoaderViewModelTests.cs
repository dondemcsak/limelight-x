using LimelightX.UI.Services;
using LimelightX.UI.ViewModels;
using Xunit;

namespace LimelightX.UI.Tests.Load;

public class FileLoaderViewModelTests
{
    private sealed class FakeFilePickerService(string? path) : IFilePickerService
    {
        public Task<string?> PickCnlFileAsync() => Task.FromResult(path);
    }

    [Fact]
    public async Task LoadFileCommand_ValidFile_PopulatesContentAndRecentFiles()
    {
        var tempPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempPath, "Load the article from \"a.txt\".\nSummarize it.", TestContext.Current.CancellationToken);

        try
        {
            var viewModel = new FileLoaderViewModel(new FakeFilePickerService(tempPath));
            string? emitted = null;
            viewModel.FileLoaded += content => emitted = content;

            await viewModel.LoadFileCommand.ExecuteAsync(tempPath);

            Assert.Equal(tempPath, viewModel.SelectedFilePath);
            Assert.Contains("Summarize it.", viewModel.FileContent);
            Assert.Equal(tempPath, viewModel.RecentFiles[0]);
            Assert.Empty(viewModel.Errors);
            Assert.Equal(viewModel.FileContent, emitted);
            Assert.False(viewModel.IsLoading);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task LoadFileCommand_MissingFile_AddsErrorAndLeavesContentNull()
    {
        var viewModel = new FileLoaderViewModel(new FakeFilePickerService(null));

        await viewModel.LoadFileCommand.ExecuteAsync(@"C:\definitely\does\not\exist.llx");

        Assert.Null(viewModel.FileContent);
        Assert.Single(viewModel.Errors);
        Assert.Equal("ERR_FILE_NOT_FOUND", viewModel.Errors[0].Code);
    }

    [Fact]
    public async Task OpenFileCommand_UserCancels_DoesNothing()
    {
        var viewModel = new FileLoaderViewModel(new FakeFilePickerService(null));

        await viewModel.OpenFileCommand.ExecuteAsync(null);

        Assert.Null(viewModel.FileContent);
        Assert.Empty(viewModel.Errors);
    }
}
