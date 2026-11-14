namespace Tdt.Core.Models;

/// <summary>The difference between the file lists of two torrents.</summary>
/// <param name="Added">Paths present in the new torrent only, ordinal sorted.</param>
/// <param name="Removed">Paths present in the old torrent only, ordinal sorted.</param>
public sealed record TorrentDiff(IReadOnlyList<string> Added, IReadOnlyList<string> Removed)
{
    public bool IsEmpty => Added.Count == 0 && Removed.Count == 0;

    /// <summary>
    /// Compares two file lists as sets of paths. Order is irrelevant and
    /// comparison is ordinal, matching the previous implementation.
    /// </summary>
    public static TorrentDiff Between(IEnumerable<string> oldFiles, IEnumerable<string> newFiles)
    {
        ArgumentNullException.ThrowIfNull(oldFiles);
        ArgumentNullException.ThrowIfNull(newFiles);

        var before = new HashSet<string>(oldFiles, StringComparer.Ordinal);
        var after = new HashSet<string>(newFiles, StringComparer.Ordinal);

        var added = after.Except(before, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var removed = before.Except(after, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        return new TorrentDiff(added, removed);
    }
}
