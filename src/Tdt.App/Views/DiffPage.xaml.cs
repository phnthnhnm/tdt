using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tdt.App.Services;
using Tdt.App.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Tdt.App.Views;

public sealed partial class DiffPage : Page
{
    public DiffPage()
    {
        ViewModel = new DiffViewModel(
            App.Services,
            new DialogService(() => XamlRoot),
            App.MainWindow ?? throw new InvalidOperationException("The main window is not available."));

        InitializeComponent();
    }

    public DiffViewModel ViewModel { get; }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        ShowDropTarget(true);
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        ShowDropTarget(false);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Copy;
        if (e.DragUIOverride is not null)
        {
            e.DragUIOverride.Caption = "Compare this torrent";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
        }

        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        ShowDropTarget(false);

        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var deferral = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var torrent = items
                .OfType<StorageFile>()
                .FirstOrDefault(file => string.Equals(
                    file.FileType, ".torrent", StringComparison.OrdinalIgnoreCase));

            if (torrent is not null)
            {
                await ViewModel.LoadTorrentAsync(torrent.Path);
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void ShowDropTarget(bool visible)
    {
        DropOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
}
