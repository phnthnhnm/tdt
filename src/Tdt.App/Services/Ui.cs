using Microsoft.UI.Xaml;

namespace Tdt.App.Services;

/// <summary>Boolean to visibility helpers used by x:Bind function bindings.</summary>
public static class Ui
{
    public static Visibility Visible(bool value)
    {
        return value ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility VisibleWhenFalse(bool value)
    {
        return value ? Visibility.Collapsed : Visibility.Visible;
    }
}
