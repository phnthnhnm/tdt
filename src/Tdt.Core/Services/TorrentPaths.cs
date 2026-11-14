namespace Tdt.Core.Services;

/// <summary>Path helpers shared by the diff and the qBittorrent selection.</summary>
public static class TorrentPaths
{
    /// <summary>
    /// Normalizes a qBittorrent content path, which always uses forward
    /// slashes, into a platform path.
    /// </summary>
    public static string Normalize(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.Replace('/', Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// The file name of a torrent path, ignoring the torrent root and any
    /// separator style.
    /// </summary>
    public static string GetFileName(string path)
    {
        return Path.GetFileName(Normalize(path));
    }

    /// <summary>
    /// True when <paramref name="candidate"/> names the same file as
    /// <paramref name="target"/>. The torrent root differs between the
    /// archived and the new torrent often enough that only the file name is
    /// reliable.
    /// </summary>
    public static bool IsSameFile(string candidate, string target)
    {
        var candidateName = GetFileName(candidate);
        return candidateName.Length > 0 &&
            string.Equals(candidateName, GetFileName(target), StringComparison.Ordinal);
    }
}
