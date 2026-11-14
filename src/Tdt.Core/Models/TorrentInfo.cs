namespace Tdt.Core.Models;

/// <summary>A single file entry inside a torrent.</summary>
/// <param name="Path">Torrent name plus path segments, joined for this platform.</param>
/// <param name="Length">Declared file size in bytes.</param>
public sealed record TorrentFileEntry(string Path, long Length);

/// <summary>The parts of a torrent metainfo file this app uses.</summary>
/// <param name="Name">The root name of the torrent.</param>
/// <param name="Files">Every file entry, in torrent order.</param>
/// <param name="TotalLength">Sum of the declared file sizes.</param>
public sealed record TorrentInfo(
    string Name,
    IReadOnlyList<TorrentFileEntry> Files,
    long TotalLength)
{
    /// <summary>The file paths, which is what the diff compares.</summary>
    public IEnumerable<string> Paths => Files.Select(file => file.Path);
}
