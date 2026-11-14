using Microsoft.UI.Xaml;
using Tdt.App.Services;

namespace Tdt.App;

public partial class App : Application
{
    private Window? _mainWindow;

    public App()
    {
        InitializeComponent();
        Services = AppServices.CreateDefault();
    }

    /// <summary>Long-lived services shared by every window.</summary>
    public static AppServices Services { get; private set; } = null!;

    /// <summary>The main window, used by pages that need a picker owner.</summary>
    public static Window? MainWindow { get; internal set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mainWindow = new MainWindow();
        _mainWindow.Activate();
    }
}
