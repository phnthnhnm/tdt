using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Tdt.App.Services;

/// <summary>Window chrome shared by the main and settings windows.</summary>
public static class WindowChrome
{
    /// <summary>
    /// Uses a Mica backdrop where the OS supports it and leaves the solid page
    /// background in place otherwise, so content stays readable on Windows 10.
    /// </summary>
    public static void ApplyBackdrop(Window window, Panel root)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(root);

        if (!MicaController.IsSupported())
        {
            return;
        }

        window.SystemBackdrop = new MicaBackdrop();
        root.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
    }
}
