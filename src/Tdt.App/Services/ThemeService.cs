using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Tdt.Core.Models;

namespace Tdt.App.Services;

/// <summary>
/// Applies the stored theme to every open window, including the title bar
/// caption buttons, which otherwise keep following the system theme.
/// </summary>
public sealed class ThemeService
{
    private readonly List<Window> _windows = [];

    public AppTheme Current { get; private set; } = AppTheme.System;

    public void Register(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (!_windows.Contains(window))
        {
            _windows.Add(window);
        }

        ApplyTo(window);
    }

    public void Unregister(Window window)
    {
        _windows.Remove(window);
    }

    public void SetTheme(AppTheme theme)
    {
        Current = theme;
        foreach (var window in _windows.ToArray())
        {
            ApplyTo(window);
        }
    }

    private void ApplyTo(Window window)
    {
        if (window.Content is not FrameworkElement root)
        {
            return;
        }

        root.RequestedTheme = Current switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        ApplyCaptionButtons(window, root);
    }

    private void ApplyCaptionButtons(Window window, FrameworkElement root)
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = window.AppWindow?.TitleBar;
        if (titleBar is null)
        {
            return;
        }

        var isDark = Current switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => root.ActualTheme == ElementTheme.Dark,
        };

        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = isDark ? Colors.White : Colors.Black;
        titleBar.ButtonInactiveForegroundColor = isDark
            ? Windows.UI.Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)
            : Windows.UI.Color.FromArgb(0x90, 0x00, 0x00, 0x00);
        titleBar.ButtonHoverBackgroundColor = isDark
            ? Windows.UI.Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)
            : Windows.UI.Color.FromArgb(0x10, 0x00, 0x00, 0x00);
        titleBar.ButtonHoverForegroundColor = isDark ? Colors.White : Colors.Black;
        titleBar.ButtonPressedBackgroundColor = isDark
            ? Windows.UI.Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)
            : Windows.UI.Color.FromArgb(0x18, 0x00, 0x00, 0x00);
        titleBar.ButtonPressedForegroundColor = isDark ? Colors.White : Colors.Black;
    }
}
