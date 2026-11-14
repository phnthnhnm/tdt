using Tdt.Core.Models;
using Tdt.Core.Services;

namespace Tdt.Core.Tests;

public sealed class SettingsBackupTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "tdt-backup-tests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leaked temp directory must not fail a test run.
        }
    }

    private JsonSettingsStore CreateStore()
    {
        return new JsonSettingsStore(
            Path.Combine(_directory, "settings"),
            new PlaintextSecretProtector());
    }

    private string BackupPath => Path.Combine(_directory, "backup.json");

    [Fact]
    public void ExportWritesTheStoredSettings()
    {
        var store = CreateStore();
        store.Save(new AppSettings { Theme = AppTheme.Dark, DataFolder = @"C:\Torrents" });

        store.Export(BackupPath);

        Assert.True(File.Exists(BackupPath));
        Assert.Contains("Torrents", File.ReadAllText(BackupPath), StringComparison.Ordinal);
    }

    [Fact]
    public void ExportCreatesABackupEvenWhenNothingWasSavedYet()
    {
        var store = CreateStore();

        store.Export(BackupPath);

        Assert.True(File.Exists(BackupPath));
        Assert.True(File.Exists(store.FilePath));
    }

    [Fact]
    public void ImportReplacesTheSettings()
    {
        var source = CreateStore();
        source.Save(new AppSettings
        {
            Theme = AppTheme.Light,
            DataFolder = @"D:\Old",
            QBittorrent = new QBittorrentSettings { Host = "seedbox", Blacklist = ["sample"] },
        });
        source.Export(BackupPath);

        var target = new JsonSettingsStore(
            Path.Combine(_directory, "other"),
            new PlaintextSecretProtector());
        target.Import(BackupPath);

        var loaded = target.Load();
        Assert.Equal(AppTheme.Light, loaded.Theme);
        Assert.Equal(@"D:\Old", loaded.DataFolder);
        Assert.Equal("seedbox", loaded.QBittorrent.Host);
        Assert.Equal(["sample"], loaded.QBittorrent.Blacklist);
    }

    [Fact]
    public void ImportRejectsAFileThatIsNotASettingsBackup()
    {
        var store = CreateStore();
        Directory.CreateDirectory(_directory);
        File.WriteAllText(BackupPath, "{ not a settings file");
        store.Save(new AppSettings { DataFolder = @"C:\Keep" });

        Assert.Throws<InvalidDataException>(() => store.Import(BackupPath));

        Assert.Equal(@"C:\Keep", store.Load().DataFolder);
    }

    [Fact]
    public void ImportRejectsAnEmptyFile()
    {
        var store = CreateStore();
        Directory.CreateDirectory(_directory);
        File.WriteAllText(BackupPath, string.Empty);

        Assert.Throws<InvalidDataException>(() => store.Import(BackupPath));
    }

    [Fact]
    public void ImportRejectsABackupFromANewerVersion()
    {
        var store = CreateStore();
        Directory.CreateDirectory(_directory);
        File.WriteAllText(BackupPath, """{"version": 99, "dataFolder": "D:\\New"}""");

        Assert.Throws<InvalidDataException>(() => store.Import(BackupPath));
    }
}
