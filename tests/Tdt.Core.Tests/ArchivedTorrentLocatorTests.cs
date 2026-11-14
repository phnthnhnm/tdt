using Tdt.Core.Services;
using Tdt.Core.Tests.Support;

namespace Tdt.Core.Tests;

public sealed class ArchivedTorrentLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "tdt-locator-tests",
        Guid.NewGuid().ToString("N"));

    public ArchivedTorrentLocatorTests()
    {
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(Inbox);
    }

    private string DataFolder => Path.Combine(_root, "data");

    private string Inbox => Path.Combine(_root, "inbox");

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leaked temp directory must not fail a test run.
        }
    }

    private static ArchivedTorrentLocator CreateLocator() => new(new TorrentService());

    private static string Write(byte[] bytes, string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static byte[] Torrent(string name, params (string[] Segments, long Length)[] files)
    {
        var specs = files
            .Select(file => new BencodeFixture.FileSpec(file.Segments, file.Length))
            .ToArray();
        return BencodeFixture.MultiFileTorrent(name, specs).Torrent;
    }

    [Fact]
    public async Task ReportsAnUnconfiguredDataFolder()
    {
        var released = Write(Torrent("Release", (["001.cbz"], 1)), Inbox, "release.torrent");

        var result = await CreateLocator().LocateAsync(released, string.Empty);

        Assert.Equal(ArchivedTorrentStatus.DataFolderNotConfigured, result.Status);
        Assert.False(result.IsFound);
    }

    [Fact]
    public async Task ReportsAMissingDataFolder()
    {
        var released = Write(Torrent("Release", (["001.cbz"], 1)), Inbox, "release.torrent");

        var result = await CreateLocator().LocateAsync(
            released,
            Path.Combine(_root, "does-not-exist"));

        Assert.Equal(ArchivedTorrentStatus.DataFolderMissing, result.Status);
    }

    [Fact]
    public async Task FindsTheTorrentWithTheSameFileName()
    {
        var archived = Write(Torrent("Release", (["001.cbz"], 1)), DataFolder, "release.torrent");
        var released = Write(Torrent("Release", (["001.cbz"], 2)), Inbox, "release.torrent");

        var result = await CreateLocator().LocateAsync(released, DataFolder);

        Assert.True(result.IsFound);
        Assert.Equal(archived, result.ArchivedPath);
        Assert.NotNull(result.Released);
        Assert.Equal("Release", result.Released!.Name);
    }

    [Fact]
    public async Task FallsBackToTheTorrentNameWhenTheFileNameDiffers()
    {
        var archived = Write(Torrent("Release", (["001.cbz"], 1)), DataFolder, "release.torrent");
        var released = Write(Torrent("Release", (["001.cbz"], 2)), Inbox, "release (1).torrent");

        var result = await CreateLocator().LocateAsync(released, DataFolder);

        Assert.True(result.IsFound);
        Assert.Equal(archived, result.ArchivedPath);
    }

    [Fact]
    public async Task IgnoresAnIdenticalTorrentWithADifferentFileName()
    {
        var bytes = Torrent("Release", (["001.cbz"], 1));
        Write(bytes, DataFolder, "copy.torrent");
        var released = Write(bytes, Inbox, "release.torrent");

        var result = await CreateLocator().LocateAsync(released, DataFolder);

        Assert.Equal(ArchivedTorrentStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task SkipsFilesThatAreNotTorrents()
    {
        File.WriteAllText(Path.Combine(DataFolder, "broken.torrent"), "this is not bencode");
        var archived = Write(Torrent("Release", (["001.cbz"], 1)), DataFolder, "archived.torrent");
        var released = Write(Torrent("Release", (["001.cbz"], 2)), Inbox, "release.torrent");

        var result = await CreateLocator().LocateAsync(released, DataFolder);

        Assert.True(result.IsFound);
        Assert.Equal(archived, result.ArchivedPath);
    }

    [Fact]
    public async Task ReturnsNotFoundWhenNothingMatches()
    {
        Write(Torrent("Something else", (["001.cbz"], 1)), DataFolder, "other.torrent");
        var released = Write(Torrent("Release", (["001.cbz"], 2)), Inbox, "release.torrent");

        var result = await CreateLocator().LocateAsync(released, DataFolder);

        Assert.Equal(ArchivedTorrentStatus.NotFound, result.Status);
        Assert.NotNull(result.Released);
    }

    [Fact]
    public async Task DoesNotTreatTheReleasedFileAsItsOwnArchive()
    {
        var released = Write(Torrent("Release", (["001.cbz"], 1)), DataFolder, "release.torrent");

        var result = await CreateLocator().LocateAsync(released, DataFolder);

        Assert.Equal(ArchivedTorrentStatus.NotFound, result.Status);
    }
}
