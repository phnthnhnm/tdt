using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Tdt.App.Services;
using Tdt.App.Views;
using Tdt.Core.Models;
using Windows.Graphics;

namespace Tdt.App;

public sealed partial class MainWindow : Window
{
    private const int DefaultWidth = 1100;
    private const int DefaultHeight = 820;
    private const int MinimumWidth = 760;
    private const int MinimumHeight = 520;

    public MainWindow()
    {
        InitializeComponent();

        Title = "Torrent Diff Tool";
        App.MainWindow = this;
        App.Services.Theme.Register(this);

        RootFrame.Navigate(typeof(DiffPage));

        ConfigureBackdrop();
        ConfigureTitleBar();
        RestoreOrCenter();

        Closed += OnClosed;
    }

    private void ConfigureBackdrop()
    {
        WindowChrome.ApplyBackdrop(this, RootGrid);
    }

    private void ConfigureTitleBar()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        // Leave the caption buttons uncovered by the drag region.
        AppTitleBar.Padding = new Thickness(16, 0, AppWindow.TitleBar.RightInset, 0);
    }

    private void RestoreOrCenter()
    {
        var appWindow = AppWindow;
        var area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var work = area?.WorkArea ?? new RectInt32(0, 0, 1920, 1080);
        var saved = App.Services.Settings.Window;

        var width = saved.HasBounds
            ? Math.Clamp(saved.Width, MinimumWidth, Math.Max(MinimumWidth, work.Width))
            : Math.Min(DefaultWidth, Math.Max(MinimumWidth, work.Width - 80));
        var height = saved.HasBounds
            ? Math.Clamp(saved.Height, MinimumHeight, Math.Max(MinimumHeight, work.Height))
            : Math.Min(DefaultHeight, Math.Max(MinimumHeight, work.Height - 80));

        int x;
        int y;

        if (saved.HasBounds && IsOnScreen(work, saved))
        {
            x = saved.X;
            y = saved.Y;
        }
        else
        {
            x = work.X + Math.Max(0, (work.Width - width) / 2);
            y = work.Y + Math.Max(0, (work.Height - height) / 2);
        }

        appWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    /// <summary>True when enough of the saved rectangle is on this display.</summary>
    private static bool IsOnScreen(RectInt32 work, WindowPlacement saved)
    {
        var right = saved.X + Math.Max(saved.Width, MinimumWidth);
        var bottom = saved.Y + Math.Max(saved.Height, MinimumHeight);

        return saved.X < work.X + work.Width
            && right > work.X
            && saved.Y < work.Y + work.Height
            && bottom > work.Y;
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        App.Services.Theme.Unregister(this);

        var position = AppWindow.Position;
        var size = AppWindow.Size;

        try
        {
            App.Services.SaveWindowBounds(size.Width, size.Height, position.X, position.Y);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Failing to remember the geometry must never block shutdown.
        }
    }
}
