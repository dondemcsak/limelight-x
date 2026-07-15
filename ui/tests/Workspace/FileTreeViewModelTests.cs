using LimelightX.UI.ViewModels.Workspace;
using Xunit;

namespace LimelightX.UI.Tests.Workspace;

public class FileTreeViewModelTests
{
    private static string CreateTempFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), "llx-tree-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void OpenRoot_ScansTopLevelDirectoriesBeforeFiles_Alphabetically()
    {
        var root = CreateTempFolder();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "zzz-folder"));
            Directory.CreateDirectory(Path.Combine(root, "aaa-folder"));
            File.WriteAllText(Path.Combine(root, "bbb.llx"), "content");
            File.WriteAllText(Path.Combine(root, "aaa.txt"), "content");

            var viewModel = new FileTreeViewModel();
            viewModel.OpenRoot(root);

            Assert.Equal(4, viewModel.Nodes.Count);
            Assert.True(viewModel.Nodes[0].IsDirectory);
            Assert.Equal("aaa-folder", viewModel.Nodes[0].Name);
            Assert.True(viewModel.Nodes[1].IsDirectory);
            Assert.Equal("zzz-folder", viewModel.Nodes[1].Name);
            Assert.False(viewModel.Nodes[2].IsDirectory);
            Assert.Equal("aaa.txt", viewModel.Nodes[2].Name);
            Assert.False(viewModel.Nodes[3].IsDirectory);
            Assert.Equal("bbb.llx", viewModel.Nodes[3].Name);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void OpenRoot_DirectoryNode_ChildrenNotLoadedUntilExpanded()
    {
        var root = CreateTempFolder();
        try
        {
            var subfolder = Path.Combine(root, "sub");
            Directory.CreateDirectory(subfolder);
            File.WriteAllText(Path.Combine(subfolder, "nested.txt"), "content");

            var viewModel = new FileTreeViewModel();
            viewModel.OpenRoot(root);

            var subNode = Assert.Single(viewModel.Nodes);
            Assert.Empty(subNode.Children);

            subNode.IsExpanded = true;

            Assert.Single(subNode.Children);
            Assert.Equal("nested.txt", subNode.Children[0].Name);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void OpenRoot_ReExpanding_DoesNotRescanOrDuplicate()
    {
        var root = CreateTempFolder();
        try
        {
            var subfolder = Path.Combine(root, "sub");
            Directory.CreateDirectory(subfolder);
            File.WriteAllText(Path.Combine(subfolder, "nested.txt"), "content");

            var viewModel = new FileTreeViewModel();
            viewModel.OpenRoot(root);
            var subNode = viewModel.Nodes[0];

            subNode.IsExpanded = true;
            subNode.IsExpanded = false;
            subNode.IsExpanded = true;

            Assert.Single(subNode.Children);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void OpenRoot_UnreadableDirectory_SurfacesUiErrorInsteadOfThrowing()
    {
        // A nonexistent path is the simplest reliable way to trigger the
        // catch branch across environments (no need to fiddle with real ACLs).
        var missingRoot = Path.Combine(Path.GetTempPath(), "llx-tree-tests-missing-" + Guid.NewGuid().ToString("N"));

        var viewModel = new FileTreeViewModel();
        viewModel.OpenRoot(missingRoot);

        Assert.Empty(viewModel.Nodes);
        Assert.Single(viewModel.Errors);
        Assert.Equal("ERR_FILE_TREE_READ", viewModel.Errors[0].Code);
    }
}
