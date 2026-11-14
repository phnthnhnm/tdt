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
        IQBittorrentClient qBittorrent,
        TorrentAddService adder,
        IFilePickerService pickers,
        ThemeService theme,
        AppSettings settings)
    {
        SettingsStore = settingsStore;
        Torrents = torrents;
        QBittorrent = qBittorrent;
        Adder = adder;
        Pickers = pickers;
        Theme = theme;
        Settings = settings;
    }

    public ISettingsStore SettingsStore { get; }

    public ITorrentService Torrents { get; }

    public IQBittorrentClient QBittorrent { get; }

    public TorrentAddService Adder { get; }

    public IFilePickerService Pickers { get; }

    public ThemeService Theme { get; }

    /// <summary>The settings snapshot every caller reads from.</summary>
    public AppSettings Settings { get; private set; }

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
            qBittorrent,
            new TorrentAddService(qBittorrent, torrents),
            new FilePickerService(),
            theme,
            settings);
    }

    /// <summary>Persists the settings and applies the parts services cache.</summary>
    public void UpdateSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        SettingsStore.Save(settings);
        Settings = settings;
        QBittorrent.SetApiKey(settings.QBittorrent.ApiKey);
        Theme.SetTheme(settings.Theme);
    }

    /// <summary>Re-reads the settings file and re-applies its side effects.</summary>
    public void ReloadSettings()
    {
        Settings = SettingsStore.Load();
        QBittorrent.SetApiKey(Settings.QBittorrent.ApiKey);
        Theme.SetTheme(Settings.Theme);
    }

    /// <summary>Deletes the settings file and falls back to first-run defaults.</summary>
    public void ResetSettings()
    {
        SettingsStore.Reset();
        ReloadSettings();
    }
}
