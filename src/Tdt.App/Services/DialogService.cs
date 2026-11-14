using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tdt.Core.Services;

namespace Tdt.App.Services;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string primaryButtonText, string closeButtonText);

    Task ShowMessageAsync(string title, string message);

    Task<bool> ConfirmAddTorrentAsync(TorrentAddPlan plan);
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

    public async Task<bool> ConfirmAddTorrentAsync(TorrentAddPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var panel = new StackPanel { Spacing = 12 };

        panel.Children.Add(new TextBlock
        {
            Text = $"Add \"{plan.Torrent.Name}\" to qBittorrent?",
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
            TextWrapping = TextWrapping.Wrap,
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"{plan.SelectedCount} file(s) will be selected and the torrent is added paused.",
            TextWrapping = TextWrapping.Wrap,
        });

        if (plan.SelectedFiles.Count > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Selected files",
                FontWeight = FontWeights.SemiBold,
            });
            panel.Children.Add(CreatePathList(plan.SelectedFiles, 140));
        }

        if (plan.IgnoredFiles.Count > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Ignored by the blacklist",
                FontWeight = FontWeights.SemiBold,
            });
            panel.Children.Add(CreatePathList(plan.IgnoredFiles, 90));
        }

        var dialog = CreateDialog("Add to qBittorrent", new ScrollViewer { Content = panel }, "Add", "Cancel");
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private static ListView CreatePathList(IReadOnlyList<string> paths, double height)
    {
        return new ListView
        {
            MaxHeight = height,
            SelectionMode = ListViewSelectionMode.None,
            ItemsSource = paths,
            ItemTemplate = (DataTemplate)Application.Current.Resources["PathTemplate"],
        };
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
