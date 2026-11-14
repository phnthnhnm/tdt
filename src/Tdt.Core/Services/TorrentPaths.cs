namespace Tdt.Core.Services;

/// <summary>
/// Path helpers for matching torrent-relative paths against the content paths
/// qBittorrent reports, which are relative to the torrent root and always use
/// forward slashes.
/// </summary>
public static class TorrentPaths
{
    /// <summary>Rewrites a path to the forward-slash form used by torrents.</summary>
    public static string ToContentPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.Replace('\\', '/');
    }

    /// <summary>
    /// True when a qBittorrent content path names the same file as a
    /// torrent-relative path.
    /// </summary>
    /// <remarks>
    /// qBittorrent prefixes content paths with the torrent root, so the
    /// comparison strips it when present. Windows paths are case-insensitive,
    /// hence the ordinal-ignore-case compare; a case-sensitive compare would
    /// miss files whose name casing drifted between releases.
    /// </remarks>
    public static bool MatchesContentPath(
        string contentPath,
        string torrentName,
        string relativePath)
    {
        ArgumentNullException.ThrowIfNull(contentPath);
        ArgumentNullException.ThrowIfNull(torrentName);
        ArgumentNullException.ThrowIfNull(relativePath);

        var content = ToContentPath(contentPath);
        var relative = ToContentPath(relativePath);
        if (relative.Length == 0)
        {
            return false;
        }

        if (content.Equals(relative, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var root = ToContentPath(torrentName);
        if (root.Length == 0 ||
            !content.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return content[(root.Length + 1)..].Equals(relative, StringComparison.OrdinalIgnoreCase);
    }
}
