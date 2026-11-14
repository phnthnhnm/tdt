namespace Tdt.Core.Models;

/// <summary>
/// A single file inside a torrent, relative to the torrent root. The root name
/// is deliberately not part of the path: two releases of the same content often
/// differ only in the root directory name, and that is not a file change.
/// </summary>
/// <param name="RelativePath">Path segments joined for this platform.</param>
/// <param name="Length">Declared file size in bytes.</param>
public sealed record TorrentFileEntry(string RelativePath, long Length);

/// <summary>The parts of a torrent metainfo file this app uses.</summary>
/// <param name="Name">The root name of the torrent.</param>
/// <param name="InfoHash">Lower-case hex SHA-1 of the bencoded info dictionary.</param>
/// <param name="Files">Every file entry, in torrent order.</param>
/// <param name="TotalLength">Sum of the declared file sizes.</param>
public sealed record TorrentInfo(
    string Name,
    string InfoHash,
    IReadOnlyList<TorrentFileEntry> Files,
    long TotalLength)
{
    /// <summary>The relative file paths, which is what the diff compares.</summary>
    public IEnumerable<string> RelativePaths => Files.Select(file => file.RelativePath);
}
