using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tdt.App.Services;
using Tdt.App.ViewModels;
using Windows.Graphics;

namespace Tdt.App.Views;

public sealed partial class SettingsWindow : Window
{
    private const int InitialWidth = 960;
    private const int InitialHeight = 740;

    public SettingsWindow()
    {
        InitializeComponent();

        Title = "Settings";
        ViewModel = new SettingsViewModel(
            App.Services,
            new DialogService(() => (Content as FrameworkElement)?.XamlRoot),
            this);

        App.Services.Theme.Register(this);
        WindowChrome.ApplyBackdrop(this, RootGrid);
        Closed += OnClosed;

        ResizeAndCenter();
        Navigation.SelectedItem = Navigation.MenuItems[0];
    }

    public SettingsViewModel ViewModel { get; }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        App.Services.Theme.Unregister(this);
    }

    private void OnNavigationSelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem { Tag: string tag })
        {
            return;
        }

        var pageType = tag switch
        {
            "data" => typeof(DataPage),
            "qbittorrent" => typeof(QBittorrentPage),
            "about" => typeof(AboutPage),
            _ => typeof(AppearancePage),
        };

        ContentFrame.Navigate(pageType, ViewModel);
    }

    private void ResizeAndCenter()
    {
        var appWindow = AppWindow;
        appWindow.Title = Title;
        appWindow.Resize(new SizeInt32(InitialWidth, InitialHeight));

        var area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        if (area is null)
        {
            return;
        }

        var work = area.WorkArea;
        appWindow.Move(new PointInt32(
            work.X + ((work.Width - InitialWidth) / 2),
            work.Y + ((work.Height - InitialHeight) / 2)));
    }
}
