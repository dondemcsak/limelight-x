using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace LimelightX.UI.Services;

/// <summary>
/// Concrete IFilePickerService over Avalonia's IStorageProvider. Takes a
/// lazy TopLevel accessor (rather than a direct Window reference) so it can
/// be constructed in the composition root (App.axaml.cs) before or after
/// MainWindow exists.
/// </summary>
public sealed class FilePickerService(Func<TopLevel?> topLevelAccessor) : IFilePickerService
{
    public async Task<string?> PickCnlFileAsync()
    {
        var topLevel = topLevelAccessor();
        if (topLevel is null)
        {
            return null;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open CNL File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Limelight-X CNL") { Patterns = ["*.llx"] },
            ],
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}
