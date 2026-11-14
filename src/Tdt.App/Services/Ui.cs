namespace Tdt.App.Services;

/// <summary>
/// Path-free helpers used from XAML function bindings.
/// </summary>
public static class Ui
{
    public static Microsoft.UI.Xaml.Visibility Visible(bool value)
    {
        return value ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    public static Microsoft.UI.Xaml.Visibility VisibleWhenFalse(bool value)
    {
        return value ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
    }
}
