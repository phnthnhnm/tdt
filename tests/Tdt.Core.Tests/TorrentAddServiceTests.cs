using Tdt.Core.Services;
using Tdt.Core.Tests.Support;

namespace Tdt.Core.Tests;

public sealed class TorrentAddServiceTests : IDisposable
{
    private static readonly QBittorrentConnection Connection = new("127.0.0.1", 8080, UseHttps: false);

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "tdt-add-tests",
        Guid.NewGuid().ToString("N"));

    private readonly List<TimeSpan> _delays = [];

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

    private (TorrentAddService Service, FakeQBittorrentClient Client) Create(int pollAttempts = 8)
    {
        Directory.CreateDirectory(_directory);
        var client = new FakeQBittorrentClient();
        var service = new TorrentAddService(
            client,
            new TorrentService(),
            (interval, _) =>
            {
                _delays.Add(interval);
                return Task.CompletedTask;
            },
            pollAttempts);
        return (service, client);
    }

    private string WriteTorrent(string name = "Release")
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "new.torrent");
        File.WriteAllBytes(path, BencodeFixture.MultiFileTorrent(name, ["001.cbz"], ["002.cbz"]));
        return path;
    }

    [Fact]
    public async Task PrepareReportsTheIgnoredFiles()
    {
        var (service, _) = Create();
        var torrentPath = WriteTorrent();

        var preview = await service.PrepareAsync(
            torrentPath,
            ["Release/001.cbz", "Release/sample/002.cbz"],
            ["SAMPLE"]);

        Assert.Equal("Release", preview.TorrentName);
        Assert.Equal(1, preview.SelectableCount);
        var ignored = Assert.Single(preview.IgnoredFiles);
        Assert.Contains("sample", ignored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddSelectsOnlyTheAddedFiles()
    {
        var (service, client) = Create();
        var torrentPath = WriteTorrent();
        client.Torrents = [new QBittorrentTorrent { Name = "Release", Hash = "abc" }];
        client.Files =
        [
            new QBittorrentFileEntry { Index = 0, Name = "Release/001.cbz" },
            new QBittorrentFileEntry { Index = 1, Name = "Release/002.cbz" },
            new QBittorrentFileEntry { Index = 2, Name = "Release/003.cbz" },
        ];

        var result = await service.AddAsync(
            Connection,
            torrentPath,
            ["Release/001.cbz", "Release/003.cbz"],
            []);

        Assert.Equal("abc", result.Hash);
        Assert.Equal(3, result.FileCount);
        Assert.Equal(2, result.SelectedCount);

        Assert.Equal(2, client.PriorityCalls.Count);
        Assert.Equal(new[] { 0, 1, 2 }, client.PriorityCalls[0].Indexes);
        Assert.Equal(TorrentAddService.SkipPriority, client.PriorityCalls[0].Priority);
        Assert.Equal(new[] { 0, 2 }, client.PriorityCalls[1].Indexes);
        Assert.Equal(TorrentAddService.NormalPriority, client.PriorityCalls[1].Priority);
    }

    [Fact]
    public async Task AddSkipsBlacklistedFiles()
    {
        var (service, client) = Create();
        var torrentPath = WriteTorrent();
        client.Torrents = [new QBittorrentTorrent { Name = "Release", Hash = "abc" }];
        client.Files =
        [
            new QBittorrentFileEntry { Index = 0, Name = "Release/001.cbz" },
            new QBittorrentFileEntry { Index = 1, Name = "Release/sample/002.cbz" },
        ];

        await service.AddAsync(
            Connection,
            torrentPath,
            ["Release/001.cbz", "Release/sample/002.cbz"],
            ["sample"]);

        Assert.Equal(new[] { 0 }, client.PriorityCalls[1].Indexes);
    }

    [Fact]
    public async Task AddMatchesFilesAcrossDifferentTorrentRoots()
    {
        var (service, client) = Create();
        var torrentPath = WriteTorrent("Release.v2");
        client.Torrents = [new QBittorrentTorrent { Name = "Release.v2", Hash = "abc" }];
        client.Files = [new QBittorrentFileEntry { Index = 7, Name = "Release.v2/sub/001.cbz" }];

        var result = await service.AddAsync(
            Connection,
            torrentPath,
            [Path.Combine("Release.v1", "sub", "001.cbz")],
            []);

        Assert.Equal(1, result.SelectedCount);
        Assert.Equal(new[] { 7 }, client.PriorityCalls[1].Indexes);
    }

    [Fact]
    public async Task AddUploadsPausedWithoutASavePath()
    {
        var (service, client) = Create();
        var torrentPath = WriteTorrent();
        client.Torrents = [new QBittorrentTorrent { Name = "Release", Hash = "abc" }];

        await service.AddAsync(Connection, torrentPath, ["Release/001.cbz"], []);

        Assert.True(client.AddedPaused);
        Assert.Null(client.AddedSavePath);
        Assert.NotNull(client.AddedBytes);
    }

    [Fact]
    public async Task AddStopsTheTorrentBeforeSelectingFiles()
    {
        var (service, client) = Create();
        var torrentPath = WriteTorrent();
        client.Torrents = [new QBittorrentTorrent { Name = "Release", Hash = "abc" }];
        client.Files = [new QBittorrentFileEntry { Index = 0, Name = "Release/001.cbz" }];

        await service.AddAsync(Connection, torrentPath, ["Release/001.cbz"], []);

        Assert.Equal(1, client.StopCalls);
        Assert.Equal(1, client.StopCallsBeforeFiles);
    }

    [Fact]
    public async Task AddContinuesWhenTheStopFails()
    {
        var (service, client) = Create();
        var torrentPath = WriteTorrent();
        client.Torrents = [new QBittorrentTorrent { Name = "Release", Hash = "abc" }];
        client.Files = [new QBittorrentFileEntry { Index = 0, Name = "Release/001.cbz" }];
        client.StopThrows = true;

        var result = await service.AddAsync(Connection, torrentPath, ["Release/001.cbz"], []);

        Assert.Equal(1, result.SelectedCount);
        Assert.Equal(new[] { 0 }, client.PriorityCalls[1].Indexes);
    }

    [Fact]
    public async Task AddPollsUntilTheTorrentAppears()
    {
        var (service, client) = Create();
        var torrentPath = WriteTorrent();
        client.TorrentPollsBeforeMatch = 2;
        client.Torrents = [new QBittorrentTorrent { Name = "Release", Hash = "abc" }];

        var result = await service.AddAsync(Connection, torrentPath, [], []);

        Assert.Equal(3, client.TorrentPolls);
        Assert.Equal(2, _delays.Count);
        Assert.All(_delays, delay => Assert.Equal(TimeSpan.FromSeconds(1), delay));
        Assert.Equal("abc", result.Hash);
    }

    [Fact]
    public async Task AddFailsWhenTheTorrentNeverAppears()
    {
        var (service, client) = Create(pollAttempts: 3);
        var torrentPath = WriteTorrent();

        var error = await Assert.ThrowsAsync<QBittorrentException>(
            () => service.AddAsync(Connection, torrentPath, [], []));

        Assert.Contains("Could not find torrent", error.Message, StringComparison.Ordinal);
        Assert.Equal(3, client.TorrentPolls);
        Assert.Equal(3, _delays.Count);
        Assert.Empty(client.PriorityCalls);
    }

    [Fact]
    public async Task AddWithNoFilesSkipsPriorityCalls()
    {
        var (service, client) = Create();
        var torrentPath = WriteTorrent();
        client.Torrents = [new QBittorrentTorrent { Name = "Release", Hash = "abc" }];

        var result = await service.AddAsync(Connection, torrentPath, [], []);

        Assert.Equal(0, result.FileCount);
        Assert.Equal(0, result.SelectedCount);
        Assert.Empty(client.PriorityCalls);
    }

    [Fact]
    public async Task AddWhenNothingMatchesOnlyDeselectsEverything()
    {
        var (service, client) = Create();
        var torrentPath = WriteTorrent();
        client.Torrents = [new QBittorrentTorrent { Name = "Release", Hash = "abc" }];
        client.Files =
        [
            new QBittorrentFileEntry { Index = 0, Name = "Release/old.cbz" },
            new QBittorrentFileEntry { Index = 1, Name = "Release/older.cbz" },
        ];

        var result = await service.AddAsync(Connection, torrentPath, ["Release/new.cbz"], []);

        Assert.Equal(0, result.SelectedCount);
        var call = Assert.Single(client.PriorityCalls);
        Assert.Equal(TorrentAddService.SkipPriority, call.Priority);
    }
}
