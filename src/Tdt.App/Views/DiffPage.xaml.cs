using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tdt.App.Services;
using Tdt.App.ViewModels;

namespace Tdt.App.Views;

public sealed partial class DiffPage : Page
{
    private SettingsWindow? _settingsWindow;

    public DiffPage()
    {
        ViewModel = new DiffViewModel(
            App.Services,
            new DialogService(() => XamlRoot),
            App.MainWindow ?? throw new InvalidOperationException("The main window is not available."));

        InitializeComponent();
    }

    public DiffViewModel ViewModel { get; }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.Activate();
    }

    private async void OnReportBugClick(object sender, RoutedEventArgs e)
    {
        await Windows.System.Launcher.LaunchUriAsync(
            new Uri("https://github.com/phnthnhnm/tdt/issues/new"));
    }
}
