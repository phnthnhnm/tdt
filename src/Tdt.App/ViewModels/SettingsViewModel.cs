using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Tdt.App.Services;
using Tdt.Core.Models;
using Tdt.Core.Services;

namespace Tdt.App.ViewModels;

/// <summary>
/// Backs the settings window. Section changes are saved immediately, except the
/// qBittorrent connection, which has an explicit save and test button.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private const string SourceUrl = "https://github.com/phnthnhnm/tdt";

    private readonly AppServices _services;
    private readonly IDialogService _dialogs;
    private readonly Window _window;

    private bool _loading;

    public SettingsViewModel(AppServices services, IDialogService dialogs, Window window)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(window);

        _services = services;
        _dialogs = dialogs;
        _window = window;

        LoadFromSettings();
    }

    public ObservableCollection<string> Blacklist { get; } = [];

    public string SettingsPath => _services.SettingsStore.FilePath;

    public string VersionText { get; } = ReadVersion();

    public string CopyrightText { get; } =
        string.Create(CultureInfo.InvariantCulture, $"Copyright © {DateTime.Now.Year} Phan Thanh Nam");

    /// <summary>Why the settings fell back to defaults, shown on the Data page.</summary>
    public string? SettingsWarning => _services.SettingsWarning;

    public bool HasSettingsWarning => !string.IsNullOrEmpty(SettingsWarning);

    [ObservableProperty]
    private int _themeIndex;

    [ObservableProperty]
    private string _dataFolder = string.Empty;

    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private string _port = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private bool _useHttps;

    [ObservableProperty]
    private string _newBlacklistEntry = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _statusMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQBittorrentError))]
    private string? _qBittorrentError;

    [ObservableProperty]
    private bool _isTestingConnection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasConnectionResult))]
    private string? _connectionResult;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionSucceeded))]
    private bool _connectionFailed;

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public bool HasQBittorrentError => !string.IsNullOrEmpty(QBittorrentError);

    public bool HasConnectionResult => !string.IsNullOrEmpty(ConnectionResult);

    /// <summary>Drives the severity of the connection result info bar.</summary>
    public bool ConnectionSucceeded => HasConnectionResult && !ConnectionFailed;
    public bool ShowConnectionSuccess => HasConnectionResult && !ConnectionFailed;
    public bool ShowConnectionFailure => HasConnectionResult && ConnectionFailed;
    public bool CanTestConnection => !IsTestingConnection;



    public string DataFolderDisplay => string.IsNullOrEmpty(DataFolder) ? "Not set" : DataFolder;

    partial void OnThemeIndexChanged(int value)
    {
        if (_loading)
        {
            return;
        }

        Persist(settings => settings.Theme = (AppTheme)value);
    }

    partial void OnDataFolderChanged(string value)
    {
        OnPropertyChanged(nameof(DataFolderDisplay));
    }

    [RelayCommand]
    private async Task BrowseDataFolderAsync()
    {
        var folder = await _services.Pickers.PickFolderAsync(_window, "Use this folder");
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        DataFolder = folder;
        Persist(settings => settings.DataFolder = folder, "Data folder saved");
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        if (string.IsNullOrEmpty(DataFolder) || !Directory.Exists(DataFolder))
        {
            StatusMessage = "The data folder does not exist yet";
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = DataFolder, UseShellExecute = true });
    }

    [RelayCommand]
    private void SaveQBittorrent()
    {
        if (!TryBuildQBittorrent(out var settings, out var error))
        {
            QBittorrentError = error;
            return;
        }

        QBittorrentError = null;
        Persist(stored => stored.QBittorrent = settings, "qBittorrent settings saved");
    }

    [RelayCommand]
    private void ClearApiKey()
    {
        ApiKey = string.Empty;
        SaveQBittorrent();
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (!TryBuildQBittorrent(out var settings, out var error))
        {
            QBittorrentError = error;
            return;
        }

        QBittorrentError = null;
        ConnectionResult = null;
        ConnectionFailed = false;
        IsTestingConnection = true;

        try
        {
            var count = await QBittorrentSession.TestAsync(_services.QBittorrent, settings);
            ConnectionResult = string.Create(
                CultureInfo.InvariantCulture,
                $"Connected. {count} torrent(s) found.");
            ConnectionFailed = false;
        }
        catch (Exception ex) when (
            ex is QBittorrentException
                or HttpRequestException
                or TaskCanceledException
                or InvalidOperationException)
        {
            ConnectionResult = ex.Message;
            ConnectionFailed = true;
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    [RelayCommand]
    private void AddBlacklistEntry()
    {
        var value = NewBlacklistEntry.Trim().ToLowerInvariant();
        NewBlacklistEntry = string.Empty;

        if (value.Length == 0 || Blacklist.Contains(value, StringComparer.Ordinal))
        {
            return;
        }

        Blacklist.Add(value);
        Persist(settings => settings.QBittorrent.Blacklist = [.. Blacklist]);
    }

    public void RemoveBlacklistEntry(string entry)
    {
        if (Blacklist.Remove(entry))
        {
            Persist(settings => settings.QBittorrent.Blacklist = [.. Blacklist]);
        }
    }

    [RelayCommand]
    private async Task BackupAsync()
    {
        var suggested = string.Create(
            CultureInfo.InvariantCulture,
            $"tdt_backup_{DateTime.Now:yyyyMMddHHmmss}.json");

        var path = await _services.Pickers.PickSaveJsonAsync(_window, "Save backup", suggested);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            // Save the visible form first so the backup matches the window.
            if (TryBuildQBittorrent(out var settings, out _))
            {
                Persist(stored => stored.QBittorrent = settings);
            }

            _services.SettingsStore.Export(path);
            StatusMessage = $"Backup saved to {Path.GetFileName(path)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _dialogs.ShowMessageAsync("Backup failed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        var path = await _services.Pickers.PickJsonFileAsync(_window, "Restore backup");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (!await _dialogs.ConfirmAsync(
                "Restore settings",
                "Replace all current settings with the selected backup?",
                "Restore",
                "Cancel"))
        {
            return;
        }

        try
        {
            _services.SettingsStore.Import(path);
            _services.ReloadSettings();
            LoadFromSettings();
            StatusMessage = "Settings restored";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            await _dialogs.ShowMessageAsync("Restore failed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ResetAllAsync()
    {
        if (!await _dialogs.ConfirmAsync(
                "Reset all settings",
                "This deletes every saved setting, including the qBittorrent connection. This cannot be undone.",
                "Reset",
                "Cancel"))
        {
            return;
        }

        try
        {
            _services.ResetSettings();
            LoadFromSettings();
            StatusMessage = "All settings have been reset";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _dialogs.ShowMessageAsync("Reset failed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task OpenSourceAsync()
    {
        if (!await Windows.System.Launcher.LaunchUriAsync(new Uri(SourceUrl)))
        {
            await _dialogs.ShowMessageAsync("Could not open the link", SourceUrl);
        }
    }

    [RelayCommand]
    private void OpenSettingsFolder()
    {
        var directory = Path.GetDirectoryName(SettingsPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            StatusMessage = "The settings folder does not exist yet";
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
    }

    private bool TryBuildQBittorrent(
        out QBittorrentSettings settings,
        out string? error)
    {
        settings = new QBittorrentSettings();
        error = null;

        if (string.IsNullOrWhiteSpace(Host))
        {
            error = "Enter the host of the qBittorrent WebUI.";
            return false;
        }

        if (!int.TryParse(Port, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) ||
            port is < 1 or > 65535)
        {
            error = "The port must be a number between 1 and 65535.";
            return false;
        }

        settings = new QBittorrentSettings
        {
            Host = Host.Trim(),
            Port = port,
            Username = Username,
            Password = Password,
            ApiKey = ApiKey,
            UseHttps = UseHttps,
            Blacklist = [.. Blacklist],
        };

        return true;
    }

    private void Persist(Action<AppSettings> mutate, string? status = null)
    {
        var settings = Clone(_services.Settings);
        mutate(settings);

        try
        {
            _services.UpdateSettings(settings);
            if (status is not null)
            {
                StatusMessage = status;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            StatusMessage = $"Could not save settings: {ex.Message}";
        }
    }

    private void LoadFromSettings()
    {
        _loading = true;

        var settings = _services.Settings;
        ThemeIndex = (int)settings.Theme;
        DataFolder = settings.DataFolder;
        Host = settings.QBittorrent.Host;
        Port = settings.QBittorrent.Port.ToString(CultureInfo.InvariantCulture);
        Username = settings.QBittorrent.Username;
        Password = settings.QBittorrent.Password;
        ApiKey = settings.QBittorrent.ApiKey;
        UseHttps = settings.QBittorrent.UseHttps;

        Blacklist.Clear();
        foreach (var entry in settings.QBittorrent.Blacklist)
        {
            Blacklist.Add(entry);
        }

        _loading = false;
        OnPropertyChanged(nameof(DataFolderDisplay));
        OnPropertyChanged(nameof(SettingsWarning));
        OnPropertyChanged(nameof(HasSettingsWarning));
    }

    private static string ReadVersion()
    {
        // MinVer stamps the full version into InformationalVersion and leaves
        // AssemblyVersion at major.0.0.0, so this is the property to show.
        var informational = typeof(SettingsViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrEmpty(informational))
        {
            return "0.0.0";
        }

        // Drop build metadata such as "1.2.3+abc123".
        var metadata = informational.IndexOf('+', StringComparison.Ordinal);
        return metadata < 0 ? informational : informational[..metadata];
    }

    private static AppSettings Clone(AppSettings source)
    {
        return new AppSettings
        {
            Version = source.Version,
            Theme = source.Theme,
            DataFolder = source.DataFolder,
            Window = new WindowPlacement
            {
                Width = source.Window.Width,
                Height = source.Window.Height,
                X = source.Window.X,
                Y = source.Window.Y,
                Maximized = source.Window.Maximized,
            },
            QBittorrent = new QBittorrentSettings
            {
                Host = source.QBittorrent.Host,
                Port = source.QBittorrent.Port,
                Username = source.QBittorrent.Username,
                Password = source.QBittorrent.Password,
                ApiKey = source.QBittorrent.ApiKey,
                UseHttps = source.QBittorrent.UseHttps,
                Blacklist = [.. source.QBittorrent.Blacklist],
            },
        };
    }
}
