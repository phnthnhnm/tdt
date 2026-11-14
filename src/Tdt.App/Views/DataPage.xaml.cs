using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Tdt.App.ViewModels;

namespace Tdt.App.Views;

public sealed partial class DataPage : Page
{
    public DataPage()
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
}
