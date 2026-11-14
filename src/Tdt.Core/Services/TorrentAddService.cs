using Tdt.Core.Models;

namespace Tdt.Core.Services;

/// <summary>What the confirmation dialog shows before a torrent is added.</summary>
/// <param name="Torrent">The released torrent.</param>
/// <param name="SelectedFiles">Files that will be selected, relative to the torrent root.</param>
/// <param name="IgnoredFiles">Files the blacklist keeps out of the selection.</param>
public sealed record TorrentAddPlan(
    TorrentInfo Torrent,
    IReadOnlyList<string> SelectedFiles,
    IReadOnlyList<string> IgnoredFiles)
{
    public int SelectedCount => SelectedFiles.Count;
}

/// <summary>What the add operation actually did.</summary>
public sealed record TorrentAddResult(string Hash, int FileCount, int SelectedCount);

public interface ITorrentAddService
{
    /// <summary>Applies the blacklist to the files that need downloading.</summary>
    TorrentAddPlan Plan(
        TorrentInfo torrent,
        IReadOnlyList<string> filesToSelect,
        IReadOnlyList<string> blacklist);

    /// <summary>
    /// Adds the torrent paused and leaves only the planned files selected.
    /// </summary>
    Task<TorrentAddResult> AddAsync(
        QBittorrentConnection connection,
        string torrentPath,
        TorrentAddPlan plan,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Adds a torrent paused and selects only the planned files.
/// </summary>
/// <remarks>
/// qBittorrent has no "select these files" call, so the workflow is: add
/// paused, find the torrent by info hash, stop it explicitly, set every file to
/// priority 0, then set the planned files back to priority 1.
/// </remarks>
public sealed class TorrentAddService : ITorrentAddService
{
    /// <summary>qBittorrent priority that marks a file as skipped.</summary>
    public const int SkipPriority = 0;

    /// <summary>qBittorrent priority that marks a file as wanted.</summary>
    public const int NormalPriority = 1;

    private readonly IQBittorrentClient _client;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly int _pollAttempts;
    private readonly TimeSpan _pollInterval;

    public TorrentAddService(
        IQBittorrentClient client,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        int pollAttempts = 8,
        TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentOutOfRangeException.ThrowIfLessThan(pollAttempts, 1);

        _client = client;
        _delay = delay ?? ((interval, token) => Task.Delay(interval, token));
        _pollAttempts = pollAttempts;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
    }

    public TorrentAddPlan Plan(
        TorrentInfo torrent,
        IReadOnlyList<string> filesToSelect,
        IReadOnlyList<string> blacklist)
    {
        ArgumentNullException.ThrowIfNull(torrent);
        ArgumentNullException.ThrowIfNull(filesToSelect);
        ArgumentNullException.ThrowIfNull(blacklist);

        var entries = Blacklist.Normalize(blacklist);
        var selected = new List<string>(filesToSelect.Count);
        var ignored = new List<string>();

        foreach (var file in filesToSelect)
        {
            if (Blacklist.Matches(file, entries))
            {
                ignored.Add(file);
            }
            else
            {
                selected.Add(file);
            }
        }

        return new TorrentAddPlan(torrent, selected, ignored);
    }

    public async Task<TorrentAddResult> AddAsync(
        QBittorrentConnection connection,
        string torrentPath,
        TorrentAddPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(torrentPath);
        ArgumentNullException.ThrowIfNull(plan);

        var bytes = await File.ReadAllBytesAsync(torrentPath, cancellationToken)
            .ConfigureAwait(false);
        await _client.AddTorrentAsync(connection, bytes, savePath: null, paused: true, cancellationToken)
            .ConfigureAwait(false);

        var hash = await FindHashAsync(connection, plan.Torrent, cancellationToken)
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

        var files = await _client.GetFilesAsync(connection, hash, cancellationToken)
            .ConfigureAwait(false);

        if (files.Count > 0)
        {
            await _client.SetFilePriorityAsync(
                connection,
                hash,
                files.Select(file => file.Index).ToList(),
                SkipPriority,
                cancellationToken).ConfigureAwait(false);
        }

        // The plan already dropped blacklisted files, so only the confirmed
        // paths are selected here.
        var keep = files
            .Where(file => plan.SelectedFiles.Any(path =>
                TorrentPaths.MatchesContentPath(file.Name, plan.Torrent.Name, path)))
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
        TorrentInfo torrent,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < _pollAttempts; attempt++)
        {
            var torrents = await _client.GetTorrentsAsync(connection, cancellationToken)
                .ConfigureAwait(false);

            // The info hash is exact; the name is only a fallback for servers
            // that report a differently cased hash.
            var match = torrents.FirstOrDefault(
                    candidate => string.Equals(
                        candidate.Hash, torrent.InfoHash, StringComparison.OrdinalIgnoreCase))
                ?? torrents.FirstOrDefault(
                    candidate => string.Equals(
                        candidate.Name, torrent.Name, StringComparison.Ordinal));

            if (!string.IsNullOrEmpty(match?.Hash))
            {
                return match.Hash;
            }

            await _delay(_pollInterval, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }
}
