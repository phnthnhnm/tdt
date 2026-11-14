using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tdt.Core.Services;

namespace Tdt.App.Services;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string primaryButtonText, string closeButtonText);

    Task ShowMessageAsync(string title, string message);

    Task<bool> ConfirmAddTorrentAsync(TorrentAddPreview preview);
}

/// <summary>
/// Modal dialogs for one window. The XamlRoot is resolved when the dialog is
/// shown, so the service can be created before the window content is loaded.
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly Func<XamlRoot?> _xamlRoot;

    public DialogService(Func<XamlRoot?> xamlRoot)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        _xamlRoot = xamlRoot;
    }

    public async Task<bool> ConfirmAsync(
        string title,
        string message,
        string primaryButtonText,
        string closeButtonText)
    {
        var content = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
        };

        var dialog = CreateDialog(title, content, primaryButtonText, closeButtonText);
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var content = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
        };

        var dialog = CreateDialog(title, content, string.Empty, "Close");
        await dialog.ShowAsync();
    }

    public async Task<bool> ConfirmAddTorrentAsync(TorrentAddPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = $"Add torrent \"{preview.TorrentName}\" to qBittorrent and select {preview.SelectableCount} new file(s)?",
            TextWrapping = TextWrapping.Wrap,
        });

        if (preview.IgnoredFiles.Count > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Ignored files (match the blacklist):",
                FontWeight = FontWeights.SemiBold,
            });

            var list = new ListView
            {
                Height = 160,
                SelectionMode = ListViewSelectionMode.None,
                ItemsSource = preview.IgnoredFiles,
            };
            panel.Children.Add(list);
        }

        var dialog = CreateDialog("Confirm add to qBittorrent", panel, "Add", "Cancel");
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private ContentDialog CreateDialog(
        string title,
        object content,
        string primaryButtonText,
        string closeButtonText)
    {
        var xamlRoot = _xamlRoot()
            ?? throw new InvalidOperationException("The window is not ready to show a dialog.");

        return new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Close,
        };
    }
}
