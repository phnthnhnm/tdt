using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Tdt.App.Views;
using Windows.Graphics;

namespace Tdt.App;

public sealed partial class MainWindow : Window
{
    private const int InitialWidth = 1100;
    private const int InitialHeight = 800;

    public MainWindow()
    {
        InitializeComponent();

        Title = "Torrent Diff Tool";
        App.MainWindow = this;
        App.Services.Theme.Register(this);

        RootFrame.Navigate(typeof(DiffPage));

        ResizeAndCenter();
        Closed += OnClosed;
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        App.Services.Theme.Unregister(this);
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
