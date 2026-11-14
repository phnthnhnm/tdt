using Tdt.Core.Models;
using Tdt.Core.Services;

namespace Tdt.Core.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "tdt-settings-tests",
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

    private JsonSettingsStore CreateStore(ISecretProtector? protector = null)
    {
        return new JsonSettingsStore(_directory, protector ?? new PlaintextSecretProtector());
    }

    [Fact]
    public void ReturnsDefaultsWhenNoFileExists()
    {
        var store = CreateStore();

        var settings = store.Load();

        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.Equal(string.Empty, settings.DataFolder);
        Assert.Equal(8080, settings.QBittorrent.Port);
        Assert.Empty(settings.QBittorrent.Blacklist);
        Assert.False(File.Exists(store.FilePath));
    }

    [Fact]
    public void RoundTripsEveryValue()
    {
        var store = CreateStore();

        store.Save(new AppSettings
        {
            Theme = AppTheme.Dark,
            DataFolder = @"C:\Torrents\old",
            QBittorrent = new QBittorrentSettings
            {
                Host = "seedbox.local",
                Port = 9091,
                Username = "admin",
                Password = "hunter2",
                ApiKey = "qbt_key",
                UseHttps = true,
                Blacklist = ["sample", "xxx"],
            },
        });

        var loaded = store.Load();

        Assert.Equal(AppTheme.Dark, loaded.Theme);
        Assert.Equal(@"C:\Torrents\old", loaded.DataFolder);
        Assert.Equal("seedbox.local", loaded.QBittorrent.Host);
        Assert.Equal(9091, loaded.QBittorrent.Port);
        Assert.Equal("admin", loaded.QBittorrent.Username);
        Assert.Equal("hunter2", loaded.QBittorrent.Password);
        Assert.Equal("qbt_key", loaded.QBittorrent.ApiKey);
        Assert.True(loaded.QBittorrent.UseHttps);
        Assert.Equal(["sample", "xxx"], loaded.QBittorrent.Blacklist);
    }

    [Fact]
    public void WritesSecretsThroughTheProtector()
    {
        var store = CreateStore(new DpapiSecretProtector());

        store.Save(new AppSettings
        {
            QBittorrent = new QBittorrentSettings
            {
                Password = "hunter2",
                ApiKey = "qbt_plain_key",
            },
        });

        var onDisk = File.ReadAllText(store.FilePath);

        Assert.DoesNotContain("hunter2", onDisk, StringComparison.Ordinal);
        Assert.DoesNotContain("qbt_plain_key", onDisk, StringComparison.Ordinal);
        Assert.Equal("hunter2", store.Load().QBittorrent.Password);
        Assert.Equal("qbt_plain_key", store.Load().QBittorrent.ApiKey);
    }

    [Fact]
    public void ReturnsDefaultsForMalformedJson()
    {
        var store = CreateStore();
        Directory.CreateDirectory(_directory);
        File.WriteAllText(store.FilePath, "{ this is not json");

        var settings = store.Load();

        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.Equal(8080, settings.QBittorrent.Port);
    }

    [Fact]
    public void KeepsDefaultsForMissingProperties()
    {
        var store = CreateStore();
        Directory.CreateDirectory(_directory);
        File.WriteAllText(store.FilePath, """{"theme":"Dark"}""");

        var settings = store.Load();

        Assert.Equal(AppTheme.Dark, settings.Theme);
        Assert.Equal(8080, settings.QBittorrent.Port);
        Assert.Empty(settings.QBittorrent.Host);
    }

    [Fact]
    public void CreatesTheDirectoryOnSave()
    {
        var store = CreateStore();
        Assert.False(Directory.Exists(_directory));

        store.Save(new AppSettings { Theme = AppTheme.Light });

        Assert.True(File.Exists(store.FilePath));
    }

    [Fact]
    public void LeavesNoTemporaryFileBehind()
    {
        var store = CreateStore();

        store.Save(new AppSettings { DataFolder = @"C:\tmp" });

        Assert.False(File.Exists(store.FilePath + ".tmp"));
    }

    [Fact]
    public void ResetDeletesTheFile()
    {
        var store = CreateStore();
        store.Save(new AppSettings { Theme = AppTheme.Dark });

        store.Reset();

        Assert.False(File.Exists(store.FilePath));
        Assert.Equal(AppTheme.System, store.Load().Theme);
    }
}
