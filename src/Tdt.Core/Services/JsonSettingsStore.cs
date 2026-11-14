using System.Text.Json;
using Tdt.Core.Models;

namespace Tdt.Core.Services;

public interface ISettingsStore
{
    /// <summary>Absolute path of the settings file, shown in the About page.</summary>
    string FilePath { get; }

    /// <summary>
    /// Reads the settings. A missing, unreadable, or malformed file yields
    /// defaults instead of failing the app.
    /// </summary>
    AppSettings Load();

    /// <summary>Writes the settings atomically, protecting secret fields.</summary>
    void Save(AppSettings settings);

    /// <summary>Deletes the settings file, restoring first-run defaults.</summary>
    void Reset();

    /// <summary>Writes a copy of the settings file to <paramref name="path"/>.</summary>
    void Export(string path);

    /// <summary>
    /// Replaces the settings file with a backup.
    /// </summary>
    /// <exception cref="InvalidDataException">The backup is not a settings file.</exception>
    void Import(string path);
}

/// <summary>
/// JSON settings file under <c>%APPDATA%\Tdt\settings.json</c>.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    internal const string DefaultFolderName = "Tdt";
    private const string FileName = "settings.json";

    private readonly ISecretProtector _protector;

    public JsonSettingsStore(string? directory = null, ISecretProtector? protector = null)
    {
        var root = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            DefaultFolderName);

        FilePath = Path.Combine(root, FileName);
        _protector = protector ?? new DpapiSecretProtector();
    }

    public string FilePath { get; }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);
            return settings is null ? new AppSettings() : RestoreSecrets(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var json = JsonSerializer.Serialize(ProtectSecrets(settings), SettingsJsonContext.Default.AppSettings);

        // Write beside the target and move over it, so an interrupted save
        // cannot leave a half-written settings file behind.
        WriteAtomically(json);
    }

    public void Reset()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }

    public void Export(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(FilePath))
        {
            Save(new AppSettings());
        }

        File.Copy(FilePath, path, overwrite: true);
    }

    public void Import(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        AppSettings? imported;
        try
        {
            imported = JsonSerializer.Deserialize(
                File.ReadAllText(path),
                SettingsJsonContext.Default.AppSettings);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The selected file is not a settings backup.", ex);
        }

        if (imported is null)
        {
            throw new InvalidDataException("The selected file is not a settings backup.");
        }

        WriteAtomically(JsonSerializer.Serialize(imported, SettingsJsonContext.Default.AppSettings));
    }

    private void WriteAtomically(string json)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, FilePath, overwrite: true);
    }

    private AppSettings ProtectSecrets(AppSettings source)
    {
        return new AppSettings
        {
            Version = source.Version,
            Theme = source.Theme,
            DataFolder = source.DataFolder,
            QBittorrent = new QBittorrentSettings
            {
                Host = source.QBittorrent.Host,
                Port = source.QBittorrent.Port,
                Username = source.QBittorrent.Username,
                Password = _protector.Protect(source.QBittorrent.Password),
                ApiKey = _protector.Protect(source.QBittorrent.ApiKey),
                UseHttps = source.QBittorrent.UseHttps,
                Blacklist = [.. source.QBittorrent.Blacklist],
            },
        };
    }

    private AppSettings RestoreSecrets(AppSettings stored)
    {
        stored.QBittorrent.Password = _protector.Unprotect(stored.QBittorrent.Password);
        stored.QBittorrent.ApiKey = _protector.Unprotect(stored.QBittorrent.ApiKey);
        return stored;
    }
}
