using Tdt.Core.Models;
using Tdt.Core.Services;

namespace Tdt.App.Services;

/// <summary>
/// The composition root. It owns the settings snapshot and the services that
/// outlive a single page.
/// </summary>
public sealed class AppServices
{
    private AppServices(
        ISettingsStore settingsStore,
        ITorrentService torrents,
        ITorrentComparisonService comparisons,
        IArchivedTorrentLocator locator,
        IQBittorrentClient qBittorrent,
        ITorrentAddService adder,
        IFilePickerService pickers,
        ThemeService theme,
        AppSettings settings,
        string? settingsWarning)
    {
        SettingsStore = settingsStore;
        Torrents = torrents;
        Comparisons = comparisons;
        Locator = locator;
        QBittorrent = qBittorrent;
        Adder = adder;
        Pickers = pickers;
        Theme = theme;
        Settings = settings;
        SettingsWarning = settingsWarning;
    }

    public ISettingsStore SettingsStore { get; }

    public ITorrentService Torrents { get; }

    public ITorrentComparisonService Comparisons { get; }

    public IArchivedTorrentLocator Locator { get; }

    public IQBittorrentClient QBittorrent { get; }

    public ITorrentAddService Adder { get; }

    public IFilePickerService Pickers { get; }

    public ThemeService Theme { get; }

    /// <summary>The settings snapshot every caller reads from.</summary>
    public AppSettings Settings { get; private set; }

    /// <summary>Why the settings fell back to defaults at startup, if they did.</summary>
    public string? SettingsWarning { get; private set; }

    public static AppServices CreateDefault()
    {
        var store = new JsonSettingsStore();
        var settings = store.Load();

        var http = QBittorrentClient.CreateDefaultClient(new DebugHttpHandler());
        var qBittorrent = new QBittorrentClient(http);
        qBittorrent.SetApiKey(settings.QBittorrent.ApiKey);

        var torrents = new TorrentService();
        var theme = new ThemeService();
        theme.SetTheme(settings.Theme);

        return new AppServices(
            store,
            torrents,
            new TorrentComparisonService(torrents),
            new ArchivedTorrentLocator(torrents),
            qBittorrent,
            new TorrentAddService(qBittorrent),
            new FilePickerService(),
            theme,
            settings,
            store.LastLoadWarning);
    }

    /// <summary>Persists the settings and applies the parts services cache.</summary>
    public void UpdateSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        SettingsStore.Save(settings);
        Settings = settings;
        SettingsWarning = null;
        QBittorrent.SetApiKey(settings.QBittorrent.ApiKey);
        Theme.SetTheme(settings.Theme);
    }

    /// <summary>Re-reads the settings file and re-applies its side effects.</summary>
    public void ReloadSettings()
    {
        Settings = SettingsStore.Load();
        SettingsWarning = SettingsStore.LastLoadWarning;
        QBittorrent.SetApiKey(Settings.QBittorrent.ApiKey);
        Theme.SetTheme(Settings.Theme);
    }

    /// <summary>Deletes the settings file and falls back to first-run defaults.</summary>
    public void ResetSettings()
    {
        SettingsStore.Reset();
        ReloadSettings();
    }

    /// <summary>Remembers the main window geometry for the next start.</summary>
    /// <param name="width">Restore width, never the maximized width.</param>
    /// <param name="height">Restore height, never the maximized height.</param>
    /// <param name="maximized">Whether the window was maximized when it closed.</param>
    public void SaveWindowBounds(int width, int height, int x, int y, bool maximized)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var current = Settings;
        if (current.Window.Width == width &&
            current.Window.Height == height &&
            current.Window.X == x &&
            current.Window.Y == y &&
            current.Window.Maximized == maximized)
        {
            return;
        }

        UpdateSettings(new AppSettings
        {
            Version = current.Version,
            Theme = current.Theme,
            DataFolder = current.DataFolder,
            QBittorrent = current.QBittorrent,
            Window = new WindowPlacement
            {
                Width = width,
                Height = height,
                X = x,
                Y = y,
                Maximized = maximized,
            },
        });
    }
}
