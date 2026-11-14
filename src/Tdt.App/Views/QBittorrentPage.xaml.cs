using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Tdt.App.ViewModels;

namespace Tdt.App.Views;

public sealed partial class QBittorrentPage : Page
{
    public QBittorrentPage()
    {
        InitializeComponent();
    }

    public SettingsViewModel? ViewModel { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = e.Parameter as SettingsViewModel;
        Bindings.Update();
        base.OnNavigatedTo(e);
    }

    private void OnRemoveBlacklistEntryClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string entry })
        {
            ViewModel?.RemoveBlacklistEntry(entry);
        }
    }
}
