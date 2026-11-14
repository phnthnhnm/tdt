using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Tdt.App.Services;

/// <summary>Platform file pickers, scoped to the window that owns the dialog.</summary>
public interface IFilePickerService
{
    Task<string?> PickTorrentAsync(Window window);

    Task<string?> PickFolderAsync(Window window, string dialogTitle);

    Task<string?> PickJsonFileAsync(Window window, string dialogTitle);

    Task<string?> PickSaveJsonAsync(Window window, string dialogTitle, string suggestedFileName);
}

public sealed class FilePickerService : IFilePickerService
{
    public async Task<string?> PickTorrentAsync(Window window)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.Downloads,
            ViewMode = PickerViewMode.List,
        };
        picker.FileTypeFilter.Add(".torrent");
        Initialize(picker, window);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<string?> PickFolderAsync(Window window, string dialogTitle)
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            CommitButtonText = dialogTitle,
        };
        picker.FileTypeFilter.Add("*");
        Initialize(picker, window);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    public async Task<string?> PickJsonFileAsync(Window window, string dialogTitle)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List,
            CommitButtonText = dialogTitle,
        };
        picker.FileTypeFilter.Add(".json");
        Initialize(picker, window);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<string?> PickSaveJsonAsync(
        Window window,
        string dialogTitle,
        string suggestedFileName)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName,
            CommitButtonText = dialogTitle,
        };
        picker.FileTypeChoices.Add("JSON backup", [".json"]);
        Initialize(picker, window);

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    private static void Initialize(object picker, Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = WindowNative.GetWindowHandle(window);
        InitializeWithWindow.Initialize(picker, handle);
    }
}
