using System.Text.Json;
using Tdt.Core.Models;

namespace Tdt.Core.Services;

public interface ISettingsStore
{
    /// <summary>Absolute path of the settings file, shown in the About page.</summary>
    string FilePath { get; }

    /// <summary>
    /// Why the last <see cref="Load"/> fell back to defaults, or null when the
    /// settings were read normally. A file that cannot be read is moved aside
    /// instead of being overwritten on the next save.
    /// </summary>
    string? LastLoadWarning { get; }

    /// <summary>Reads the settings; never throws.</summary>
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
    /// <exception cref="InvalidDataException">The backup is not a usable settings file.</exception>
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

    public string? LastLoadWarning { get; private set; }

    public AppSettings Load()
    {
        LastLoadWarning = null;

        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppSettings();
            }

            var settings = JsonSerializer.Deserialize(
                File.ReadAllText(FilePath),
                SettingsJsonContext.Default.AppSettings);

            if (settings is null)
            {
                return new AppSettings();
            }

            if (settings.Version > AppSettings.CurrentVersion)
            {
                var kept = Quarantine("newer");
                LastLoadWarning = kept is null
                    ? "The settings were written by a newer version and are being ignored."
                    : $"The settings were written by a newer version and were kept as {Path.GetFileName(kept)}.";
                return new AppSettings();
            }

            return RestoreSecrets(Normalize(settings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            var kept = Quarantine("corrupt");
            LastLoadWarning = kept is null
                ? "The settings file could not be read; defaults are in use."
                : $"The settings file could not be read. A copy was kept as {Path.GetFileName(kept)}.";
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var json = JsonSerializer.Serialize(
            ProtectSecrets(Normalize(settings)),
            SettingsJsonContext.Default.AppSettings);

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

        if (imported.Version > AppSettings.CurrentVersion)
        {
            throw new InvalidDataException(
                "The selected backup was written by a newer version of the app.");
        }

        WriteAtomically(JsonSerializer.Serialize(
            Normalize(imported),
            SettingsJsonContext.Default.AppSettings));
    }

    private void WriteAtomically(string json)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write beside the target and move over it, so an interrupted save
        // cannot leave a half-written settings file behind.
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, FilePath, overwrite: true);
    }

    /// <summary>
    /// Moves an unreadable or too-new file beside the settings file so the next
    /// save cannot destroy it.
    /// </summary>
    private string? Quarantine(string reason)
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            var directory = Path.GetDirectoryName(FilePath);
            if (string.IsNullOrEmpty(directory))
            {
                return null;
            }

            var name = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{Path.GetFileNameWithoutExtension(FilePath)}.{reason}-{DateTime.UtcNow:yyyyMMddTHHmmss}.json");
            var target = Path.Combine(directory, name);
            File.Move(FilePath, target);
            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Repairs a deserialized object: JSON may contain explicit nulls for nested
    /// objects and strings that this model declares as non-null.
    /// </summary>
    private static AppSettings Normalize(AppSettings settings)
    {
        settings.DataFolder ??= string.Empty;
        settings.QBittorrent ??= new QBittorrentSettings();
        settings.Window ??= new WindowPlacement();

        var qBittorrent = settings.QBittorrent;
        qBittorrent.Host ??= string.Empty;
        qBittorrent.Username ??= string.Empty;
        qBittorrent.Password ??= string.Empty;
        qBittorrent.ApiKey ??= string.Empty;
        qBittorrent.Blacklist = qBittorrent.Blacklist is null
            ? []
            : [.. qBittorrent.Blacklist.Where(entry => !string.IsNullOrWhiteSpace(entry))];

        if (settings.Version < 1)
        {
            settings.Version = AppSettings.CurrentVersion;
        }

        return settings;
    }

    private AppSettings ProtectSecrets(AppSettings source)
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
            },
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
