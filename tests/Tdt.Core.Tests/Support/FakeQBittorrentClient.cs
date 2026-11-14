using Tdt.Core.Services;

namespace Tdt.Core.Tests.Support;

/// <summary>
/// In-memory qBittorrent client. It records what the workflow asked for so the
/// tests can assert on the selection it produced.
/// </summary>
internal sealed class FakeQBittorrentClient : IQBittorrentClient
{
    public bool HasApiKey { get; private set; }

    public List<QBittorrentFileEntry> Files { get; set; } = [];

    public List<QBittorrentTorrent> Torrents { get; set; } = [];

    /// <summary>How many poll calls return an empty list before the torrent appears.</summary>
    public int TorrentPollsBeforeMatch { get; set; }

    public bool StopThrows { get; set; }

    public List<(string Hash, IReadOnlyCollection<int> Indexes, int Priority)> PriorityCalls { get; } = [];

    public List<string> LoginCalls { get; } = [];

    public List<string> BypassCalls { get; } = [];

    public byte[]? AddedBytes { get; private set; }

    public bool? AddedPaused { get; private set; }

    public string? AddedSavePath { get; private set; }

    public int StopCalls { get; private set; }

    public int TorrentPolls { get; private set; }

    public void SetApiKey(string? apiKey)
    {
        HasApiKey = !string.IsNullOrEmpty(apiKey);
    }

    public Task LoginAsync(
        QBittorrentConnection connection,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        LoginCalls.Add(username);
        return Task.CompletedTask;
    }

    public Task AddTorrentAsync(
        QBittorrentConnection connection,
        byte[] torrentBytes,
        string? savePath = null,
        bool paused = true,
        CancellationToken cancellationToken = default)
    {
        AddedBytes = torrentBytes;
        AddedSavePath = savePath;
        AddedPaused = paused;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<QBittorrentTorrent>> GetTorrentsAsync(
        QBittorrentConnection connection,
        CancellationToken cancellationToken = default)
    {
        TorrentPolls++;
        IReadOnlyList<QBittorrentTorrent> result = TorrentPolls > TorrentPollsBeforeMatch
            ? Torrents
            : [];
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<QBittorrentFileEntry>> GetFilesAsync(
        QBittorrentConnection connection,
        string hash,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<QBittorrentFileEntry>>(Files);
    }

    public Task SetFilePriorityAsync(
        QBittorrentConnection connection,
        string hash,
        IReadOnlyCollection<int> fileIndexes,
        int priority,
        CancellationToken cancellationToken = default)
    {
        PriorityCalls.Add((hash, fileIndexes, priority));
        return Task.CompletedTask;
    }

    public Task StopAsync(
        QBittorrentConnection connection,
        string hashes,
        CancellationToken cancellationToken = default)
    {
        StopCalls++;
        return StopThrows
            ? throw new QBittorrentException("Failed to stop torrent(s) (409)")
            : Task.CompletedTask;
    }

    public Task<bool> GetBypassLocalAuthAsync(
        QBittorrentConnection connection,
        CancellationToken cancellationToken = default)
    {
        BypassCalls.Add("called");
        return Task.FromResult(false);
    }
}
