namespace Tdt.Core.Services;

/// <summary>What the confirmation dialog shows before a torrent is added.</summary>
/// <param name="TorrentName">Root name of the torrent being added.</param>
/// <param name="SelectableCount">Number of new files that will be selected.</param>
/// <param name="IgnoredFiles">New files the blacklist keeps out of the selection.</param>
public sealed record TorrentAddPreview(
    string TorrentName,
    int SelectableCount,
    IReadOnlyList<string> IgnoredFiles);

/// <summary>What the add operation actually did.</summary>
public sealed record TorrentAddResult(string Hash, int FileCount, int SelectedCount);

/// <summary>
/// Adds a torrent paused and leaves only the new, non-blacklisted files
/// selected.
/// </summary>
/// <remarks>
/// qBittorrent does not offer a "select these files" call, so the workflow is:
/// add paused, find the hash, stop it explicitly, set every file to priority 0,
/// then set the files to keep back to priority 1.
/// </remarks>
public sealed class TorrentAddService
{
    /// <summary>qBittorrent priority that marks a file as skipped.</summary>
    public const int SkipPriority = 0;

    /// <summary>qBittorrent priority that marks a file as wanted.</summary>
    public const int NormalPriority = 1;

    private readonly IQBittorrentClient _client;
    private readonly ITorrentService _torrents;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly int _pollAttempts;
    private readonly TimeSpan _pollInterval;

    public TorrentAddService(
        IQBittorrentClient client,
        ITorrentService torrents,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        int pollAttempts = 8,
        TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(torrents);
        ArgumentOutOfRangeException.ThrowIfLessThan(pollAttempts, 1);

        _client = client;
        _torrents = torrents;
        _delay = delay ?? ((interval, token) => Task.Delay(interval, token));
        _pollAttempts = pollAttempts;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
    }

    /// <summary>Reads the torrent and works out which new files the blacklist excludes.</summary>
    public async Task<TorrentAddPreview> PrepareAsync(
        string torrentPath,
        IReadOnlyList<string> addedFiles,
        IReadOnlyList<string> blacklist,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(torrentPath);
        ArgumentNullException.ThrowIfNull(addedFiles);
        ArgumentNullException.ThrowIfNull(blacklist);

        var torrent = await _torrents.LoadAsync(torrentPath, cancellationToken).ConfigureAwait(false);
        var entries = Blacklist.Normalize(blacklist);
        var ignored = Blacklist.SelectMatching(addedFiles, entries);

        return new TorrentAddPreview(
            torrent.Name,
            addedFiles.Count - ignored.Count,
            ignored);
    }

    /// <summary>
    /// Adds the torrent paused, then selects only the added files that the
    /// blacklist allows.
    /// </summary>
    public async Task<TorrentAddResult> AddAsync(
        QBittorrentConnection connection,
        string torrentPath,
        IReadOnlyList<string> addedFiles,
        IReadOnlyList<string> blacklist,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(addedFiles);
        ArgumentNullException.ThrowIfNull(blacklist);

        var preview = await PrepareAsync(torrentPath, addedFiles, blacklist, cancellationToken)
            .ConfigureAwait(false);
        var entries = Blacklist.Normalize(blacklist);

        var bytes = await File.ReadAllBytesAsync(torrentPath, cancellationToken).ConfigureAwait(false);
        await _client.AddTorrentAsync(connection, bytes, savePath: null, paused: true, cancellationToken)
            .ConfigureAwait(false);

        var hash = await FindHashAsync(connection, preview.TorrentName, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new QBittorrentException("Could not find torrent in qBittorrent");

        // The paused flag is a request, not a guarantee: stop it explicitly so
        // a server that ignored the flag cannot start downloading.
        try
        {
            await _client.StopAsync(connection, hash, cancellationToken).ConfigureAwait(false);
        }
        catch (QBittorrentException)
        {
            // Non-fatal: the selection below still describes the intended files.
        }

        var files = await _client.GetFilesAsync(connection, hash, cancellationToken).ConfigureAwait(false);

        if (files.Count > 0)
        {
            await _client.SetFilePriorityAsync(
                connection,
                hash,
                files.Select(file => file.Index).ToList(),
                SkipPriority,
                cancellationToken).ConfigureAwait(false);
        }

        var keep = files
            .Where(file => !Blacklist.Matches(file.Name, entries))
            .Where(file => addedFiles.Any(added => TorrentPaths.IsSameFile(file.Name, added)))
            .Select(file => file.Index)
            .ToList();

        if (keep.Count > 0)
        {
            await _client.SetFilePriorityAsync(
                connection,
                hash,
                keep,
                NormalPriority,
                cancellationToken).ConfigureAwait(false);
        }

        return new TorrentAddResult(hash, files.Count, keep.Count);
    }

    private async Task<string?> FindHashAsync(
        QBittorrentConnection connection,
        string torrentName,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < _pollAttempts; attempt++)
        {
            var torrents = await _client.GetTorrentsAsync(connection, cancellationToken)
                .ConfigureAwait(false);

            var match = torrents.FirstOrDefault(
                torrent => string.Equals(torrent.Name, torrentName, StringComparison.Ordinal));

            if (!string.IsNullOrEmpty(match?.Hash))
            {
                return match.Hash;
            }

            await _delay(_pollInterval, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }
}
