using Tdt.Core.Models;
using Tdt.Core.Services;
using Tdt.Core.Tests.Support;
using Tdt.Core.Torrent;

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
        var client = new FakeQBittorrentClient();
        var service = new TorrentAddService(
            client,
            (interval, _) =>
            {
                _delays.Add(interval);
                return Task.CompletedTask;
            },
            pollAttempts);
        return (service, client);
    }

    private string WriteTorrent(byte[] bytes, string fileName = "new.torrent")
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, fileName);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static TorrentInfo Torrent(byte[] bytes, params (string[] Segments, long Length)[] files)
    {
        return TorrentParser.Parse(bytes);
    }

    [Fact]
    public void PlanFiltersBlacklistedFiles()
    {
        var (service, _) = Create();
        var (bytes, _) = BencodeFixture.MultiFileTorrent(
            "Release",
            new BencodeFixture.FileSpec(["001.cbz"], 100),
            new BencodeFixture.FileSpec(["sample", "002.cbz"], 100));

        var plan = service.Plan(
            Torrent(bytes),
            [BencodeFixture.Relative("001.cbz"), BencodeFixture.Relative("sample", "002.cbz")],
            ["SAMPLE"]);

        Assert.Equal([BencodeFixture.Relative("001.cbz")], plan.SelectedFiles);
        var ignored = Assert.Single(plan.IgnoredFiles);
        Assert.Equal(BencodeFixture.Relative("sample", "002.cbz"), ignored);
        Assert.Equal(1, plan.SelectedCount);
    }

    [Fact]
    public async Task AddSelectsOnlyThePlannedFiles()
    {
        var (service, client) = Create();
        var (bytes, _) = BencodeFixture.MultiFileTorrent(
            "Release",
            new BencodeFixture.FileSpec(["001.cbz"], 100),
            new BencodeFixture.FileSpec(["002.cbz"], 100),
            new BencodeFixture.FileSpec(["003.cbz"], 100));
        var torrent = Torrent(bytes);
        var path = WriteTorrent(bytes);
        client.Torrents = [new QBittorrentTorrent { Name = "Release", Hash = torrent.InfoHash }];
        client.Files =
        [
            new QBittorrentFileEntry { Index = 0, Name = "Release/001.cbz" },
            new QBittorrentFileEntry { Index = 1, Name = "Release/002.cbz" },
            new QBittorrentFileEntry { Index = 2, Name = "Release/003.cbz" },
        ];

        var plan = service.Plan(
            torrent,
            [BencodeFixture.Relative("001.cbz"), BencodeFixture.Relative("003.cbz")],
            []);

        var result = await service.AddAsync(Connection, path, plan);

        Assert.Equal(torrent.InfoHash, result.Hash);
        Assert.Equal(3, result.FileCount);
        Assert.Equal(2, result.SelectedCount);

        Assert.Equal(2, client.PriorityCalls.Count);
        Assert.Equal([0, 1, 2], client.PriorityCalls[0].Indexes);
        Assert.Equal(TorrentAddService.SkipPriority, client.PriorityCalls[0].Priority);
        Assert.Equal([0, 2], client.PriorityCalls[1].Indexes);
        Assert.Equal(TorrentAddService.NormalPriority, client.PriorityCalls[1].Priority);
    }

    [Fact]
    public async Task AddDistinguishesSiblingFoldersWithTheSameFileName()
    {
        var (service, client) = Create();
        var (bytes, _) = BencodeFixture.MultiFileTorrent(
            "Release",
            new BencodeFixture.FileSpec(["CD1", "001.cbz"], 100),
            new BencodeFixture.FileSpec(["CD2", "001.cbz"], 100));
        var torrent = Torrent(bytes);
        var path = WriteTorrent(bytes);
        client.Torrents = [new QBittorrentTorrent { Name = "Release", Hash = torrent.InfoHash }];
        client.Files =
        [
            new QBittorrentFileEntry { Index = 0, Name = "Release/CD1/001.cbz" },
            new QBittorrentFileEntry { Index = 1, Name = "Release/CD2/001.cbz" },
        ];

        var plan = service.Plan(torrent, [BencodeFixture.Relative("CD2", "001.cbz")], []);

        var result = await service.AddAsync(Connection, path, plan);

        Assert.Equal(1, result.SelectedCount);
        Assert.Equal([1], client.PriorityCalls[1].Indexes);
    }

    [Fact]
    public async Task AddMatchesFileNamesCaseInsensitively()
    {
        var (service, client) = Create();
        var (bytes, _) = BencodeFixture.MultiFileTorrent(
            "Release", new BencodeFixture.FileSpec(["001.cbz"], 100));
        var torrent = Torrent(bytes);
        var path = WriteTorrent(bytes);
        client.Torrents = [new QBittorrentTorrent { Name = "Release", Hash = torrent.InfoHash }];
        client.Files = [new QBittorrentFileEntry { Index = 4, Name = "Release/001.CBZ" }];

        var plan = service.Plan(torrent, [BencodeFixture.Relative("001.cbz")], []);

        var result = await service.AddAsync(Connection, path, plan);

        Assert.Equal(1, result.SelectedCount);
        Assert.Equal([4], client.PriorityCalls[1].Indexes);
    }

    [Fact]
    public async Task AddFindsTheTorrentByInfoHashNotByName()
    {
        var (service, client) = Create();
        var (bytes, _) = BencodeFixture.MultiFileTorrent(
            "Release", new BencodeFixture.FileSpec(["001.cbz"], 100));
        var torrent = Torrent(bytes);
        var path = WriteTorrent(bytes);
        client.Torrents =
        [
            new QBittorrentTorrent { Name = "Release", Hash = "0000000000000000000000000000000000000000" },
            new QBittorrentTorrent { Name = "Release", Hash = torrent.InfoHash },
        ];
        client.Files = [new QBittorrentFileEntry { Index = 0, Name = "Release/001.cbz" }];

        var plan = service.Plan(torrent, [BencodeFixture.Relative("001.cbz")], []);

        var result = await service.AddAsync(Connection, path, plan);

        Assert.Equal(torrent.InfoHash, result.Hash);
        Assert.All(client.PriorityCalls, call => Assert.Equal(torrent.InfoHash, call.Hash));
    }

    [Fact]
    public async Task AddFallsBackToTheTorrentName()
    {
        var (service, client) = Create();
        var (bytes, _) = BencodeFixture.MultiFileTorrent(
            "Release", new BencodeFixture.FileSpec(["001.cbz"], 100));
        var torrent = Torrent(bytes);
        var path = WriteTorrent(bytes);
        client.Torrents = [new QBittorrentTorrent { Name = "Release", Hash = "server-side-hash" }];

        var plan = service.Plan(torrent, [BencodeFixture.Relative("001.cbz")], []);

        var result = await service.AddAsync(Connection, path, plan);

        Assert.Equal("server-side-hash", result.Hash);
    }

    [Fact]
    public async Task AddUploadsPausedWithoutASavePath()
    {
        var (service, client) = Create();
        var (bytes, _) = BencodeFixture.MultiFileTorrent(
            "Release", new BencodeFixture.FileSpec(["001.cbz"], 100));
        var torrent = Torrent(bytes);
        var path = WriteTorrent(bytes);
        client.Torrents = [new QBittorrentTorrent { Name = "Release", Hash = torrent.InfoHash }];

        await service.AddAsync(Connection, path, service.Plan(torrent, [], []));

        Assert.True(client.AddedPaused);
        Assert.Null(client.AddedSavePath);
        Assert.NotNull(client.AddedBytes);
    }

    [Fact]
    public async Task AddContinuesWhenTheStopFails()
    {
        var (service, client) = Create();
        var (bytes, _) = BencodeFixture.MultiFileTorrent(
            "Release", new BencodeFixture.FileSpec(["001.cbz"], 100));
        var torrent = Torrent(bytes);
        var path = WriteTorrent(bytes);
        client.Torrents = [new QBittorrentTorrent { Name = "Release", Hash = torrent.InfoHash }];
        client.Files = [new QBittorrentFileEntry { Index = 0, Name = "Release/001.cbz" }];
        client.StopThrows = true;

        var plan = service.Plan(torrent, [BencodeFixture.Relative("001.cbz")], []);

        var result = await service.AddAsync(Connection, path, plan);

        Assert.Equal(1, result.SelectedCount);
        Assert.Equal([0], client.PriorityCalls[1].Indexes);
    }

    [Fact]
    public async Task AddPollsUntilTheTorrentAppears()
    {
        var (service, client) = Create();
        var (bytes, _) = BencodeFixture.MultiFileTorrent(
            "Release", new BencodeFixture.FileSpec(["001.cbz"], 100));
        var torrent = Torrent(bytes);
        var path = WriteTorrent(bytes);
        client.TorrentPollsBeforeMatch = 2;
        client.Torrents = [new QBittorrentTorrent { Name = "Release", Hash = torrent.InfoHash }];

        var result = await service.AddAsync(Connection, path, service.Plan(torrent, [], []));

        Assert.Equal(3, client.TorrentPolls);
        Assert.Equal(2, _delays.Count);
        Assert.All(_delays, delay => Assert.Equal(TimeSpan.FromSeconds(1), delay));
        Assert.Equal(torrent.InfoHash, result.Hash);
    }

    [Fact]
    public async Task AddFailsWhenTheTorrentNeverAppears()
    {
        var (service, client) = Create(pollAttempts: 3);
        var (bytes, _) = BencodeFixture.MultiFileTorrent(
            "Release", new BencodeFixture.FileSpec(["001.cbz"], 100));
        var torrent = Torrent(bytes);
        var path = WriteTorrent(bytes);

        var error = await Assert.ThrowsAsync<QBittorrentException>(
            () => service.AddAsync(Connection, path, service.Plan(torrent, [], [])));

        Assert.Contains("Could not find torrent", error.Message, StringComparison.Ordinal);
        Assert.Equal(3, client.TorrentPolls);
        Assert.Equal(3, _delays.Count);
        Assert.Empty(client.PriorityCalls);
    }

    [Fact]
    public async Task AddWithNoFilesSkipsPriorityCalls()
    {
        var (service, client) = Create();
        var (bytes, _) = BencodeFixture.MultiFileTorrent(
            "Release", new BencodeFixture.FileSpec(["001.cbz"], 100));
        var torrent = Torrent(bytes);
        var path = WriteTorrent(bytes);
        client.Torrents = [new QBittorrentTorrent { Name = "Release", Hash = torrent.InfoHash }];

        var result = await service.AddAsync(Connection, path, service.Plan(torrent, [], []));

        Assert.Equal(0, result.FileCount);
        Assert.Equal(0, result.SelectedCount);
        Assert.Empty(client.PriorityCalls);
    }
}
