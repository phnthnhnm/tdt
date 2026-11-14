using Microsoft.UI.Xaml;

namespace Tdt.App.Services;

/// <summary>
/// Applies the stored theme to every open window. Windows that are not yet
/// registered keep the system theme.
/// </summary>
public sealed class ThemeService
{
    private readonly List<Window> _windows = [];

    public Tdt.Core.Models.AppTheme Current { get; private set; } = Tdt.Core.Models.AppTheme.System;

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

    public void SetTheme(Tdt.Core.Models.AppTheme theme)
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
            Tdt.Core.Models.AppTheme.Light => ElementTheme.Light,
            Tdt.Core.Models.AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }
}
