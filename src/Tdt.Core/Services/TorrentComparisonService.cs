using Tdt.Core.Models;

namespace Tdt.Core.Services;

/// <summary>The archived and released torrent plus the difference between them.</summary>
public sealed record TorrentComparison(
    string ArchivedPath,
    TorrentInfo Archived,
    TorrentInfo Released,
    TorrentDiff Diff);

public interface ITorrentComparisonService
{
    /// <summary>
    /// Loads the archived torrent and compares it with an already-loaded
    /// released torrent, so the released file is not parsed twice.
    /// </summary>
    Task<TorrentComparison> CompareAsync(
        string archivedPath,
        TorrentInfo released,
        CancellationToken cancellationToken = default);
}

public sealed class TorrentComparisonService : ITorrentComparisonService
{
    private readonly ITorrentService _torrents;

    public TorrentComparisonService(ITorrentService torrents)
    {
        ArgumentNullException.ThrowIfNull(torrents);
        _torrents = torrents;
    }

    public async Task<TorrentComparison> CompareAsync(
        string archivedPath,
        TorrentInfo released,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivedPath);
        ArgumentNullException.ThrowIfNull(released);

        var archived = await _torrents.LoadAsync(archivedPath, cancellationToken)
            .ConfigureAwait(false);

        return new TorrentComparison(
            archivedPath,
            archived,
            released,
            TorrentDiff.Between(archived.Files, released.Files));
    }
}
