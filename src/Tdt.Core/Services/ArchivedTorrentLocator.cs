using Tdt.Core.Models;
using Tdt.Core.Torrent;

namespace Tdt.Core.Services;

/// <summary>Outcome of looking for the archived copy of a torrent.</summary>
public enum ArchivedTorrentStatus
{
    /// <summary>An archived torrent was found.</summary>
    Found,

    /// <summary>The data folder is not configured yet.</summary>
    DataFolderNotConfigured,

    /// <summary>The configured data folder does not exist.</summary>
    DataFolderMissing,

    /// <summary>The folder exists but holds no matching torrent.</summary>
    NotFound,
}

/// <param name="Status">Outcome of the lookup.</param>
/// <param name="ArchivedPath">Path of the archived torrent when found.</param>
/// <param name="Released">The released torrent, already parsed.</param>
public sealed record ArchivedTorrentResult(
    ArchivedTorrentStatus Status,
    string? ArchivedPath,
    TorrentInfo? Released)
{
    public bool IsFound => Status == ArchivedTorrentStatus.Found && ArchivedPath is not null;
}

public interface IArchivedTorrentLocator
{
    /// <summary>
    /// Finds the archived torrent that corresponds to a released one.
    /// </summary>
    Task<ArchivedTorrentResult> LocateAsync(
        string releasedTorrentPath,
        string dataFolder,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Locates the archived torrent for a release.
/// </summary>
/// <remarks>
/// The file name is the first choice because it is what the old workflow
/// required. When that misses (a browser saved the file as "release (1).torrent",
/// for example) the folder is scanned for a torrent with the same root name and
/// a different info hash, most recently written first. The scan is capped so a
/// folder with thousands of torrents cannot stall the UI.
/// </remarks>
public sealed class ArchivedTorrentLocator : IArchivedTorrentLocator
{
    /// <summary>How many torrents the name fallback inspects.</summary>
    public const int MaxScan = 400;

    private readonly ITorrentService _torrents;

    public ArchivedTorrentLocator(ITorrentService torrents)
    {
        ArgumentNullException.ThrowIfNull(torrents);
        _torrents = torrents;
    }

    public async Task<ArchivedTorrentResult> LocateAsync(
        string releasedTorrentPath,
        string dataFolder,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(releasedTorrentPath);

        if (string.IsNullOrWhiteSpace(dataFolder))
        {
            return new ArchivedTorrentResult(
                ArchivedTorrentStatus.DataFolderNotConfigured, null, null);
        }

        if (!Directory.Exists(dataFolder))
        {
            return new ArchivedTorrentResult(
                ArchivedTorrentStatus.DataFolderMissing, null, null);
        }

        var released = await _torrents.LoadAsync(releasedTorrentPath, cancellationToken)
            .ConfigureAwait(false);

        var sameName = Path.Combine(dataFolder, Path.GetFileName(releasedTorrentPath));
        if (File.Exists(sameName) &&
            !string.Equals(
                Path.GetFullPath(sameName),
                Path.GetFullPath(releasedTorrentPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return new ArchivedTorrentResult(ArchivedTorrentStatus.Found, sameName, released);
        }

        return await FindByNameAsync(dataFolder, releasedTorrentPath, released, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ArchivedTorrentResult> FindByNameAsync(
        string dataFolder,
        string releasedTorrentPath,
        TorrentInfo released,
        CancellationToken cancellationToken)
    {
        List<FileInfo> candidates;
        try
        {
            candidates = new DirectoryInfo(dataFolder)
                .EnumerateFiles("*.torrent")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(MaxScan)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ArchivedTorrentResult(ArchivedTorrentStatus.DataFolderMissing, null, null);
        }

        var releasedFullPath = Path.GetFullPath(releasedTorrentPath);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.Equals(candidate.FullName, releasedFullPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            TorrentInfo archived;
            try
            {
                archived = await _torrents.LoadAsync(candidate.FullName, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TorrentFormatException)
            {
                continue;
            }

            if (!string.Equals(archived.Name, released.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(archived.InfoHash, released.InfoHash, StringComparison.OrdinalIgnoreCase))
            {
                // Same torrent, just renamed on disk; there is nothing to compare.
                continue;
            }

            return new ArchivedTorrentResult(
                ArchivedTorrentStatus.Found, candidate.FullName, released);
        }

        return new ArchivedTorrentResult(ArchivedTorrentStatus.NotFound, null, released);
    }
}
