namespace Tdt.Core.Models;

/// <summary>One file that differs between the two torrents.</summary>
/// <param name="Path">Path relative to the torrent root.</param>
/// <param name="PreviousLength">Size in the archived torrent, or null when the file is new.</param>
/// <param name="NewLength">Size in the new torrent, or null when the file was removed.</param>
public sealed record TorrentFileChange(string Path, long? PreviousLength, long? NewLength)
{
    /// <summary>Size difference in bytes; a removed file reports a negative delta.</summary>
    public long SizeDelta => (NewLength ?? 0) - (PreviousLength ?? 0);
}

/// <summary>
/// The difference between the file lists of two torrents, compared by relative
/// path. A path that exists in both with a different size is a change, not a
/// match: the release replaced the file, so it has to be downloaded again.
/// </summary>
public sealed record TorrentDiff(
    IReadOnlyList<TorrentFileChange> Added,
    IReadOnlyList<TorrentFileChange> Removed,
    IReadOnlyList<TorrentFileChange> Changed)
{
    public static TorrentDiff Empty { get; } = new([], [], []);

    public bool IsEmpty => Added.Count == 0 && Removed.Count == 0 && Changed.Count == 0;

    /// <summary>Files an update needs to download: additions plus replacements.</summary>
    public IReadOnlyList<TorrentFileChange> ToDownload => [.. Added, .. Changed];

    public int Count => Added.Count + Removed.Count + Changed.Count;

    /// <summary>
    /// Compares two file lists by relative path, ordinal and case-sensitive.
    /// Each list is sorted by path.
    /// </summary>
    public static TorrentDiff Between(
        IEnumerable<TorrentFileEntry> archived,
        IEnumerable<TorrentFileEntry> released)
    {
        ArgumentNullException.ThrowIfNull(archived);
        ArgumentNullException.ThrowIfNull(released);

        var before = ToLengthMap(archived);
        var after = ToLengthMap(released);

        var added = new List<TorrentFileChange>();
        var changed = new List<TorrentFileChange>();

        foreach (var (path, length) in after)
        {
            if (!before.TryGetValue(path, out var previousLength))
            {
                added.Add(new TorrentFileChange(path, null, length));
            }
            else if (previousLength != length)
            {
                changed.Add(new TorrentFileChange(path, previousLength, length));
            }
        }

        var removed = before
            .Where(pair => !after.ContainsKey(pair.Key))
            .Select(pair => new TorrentFileChange(pair.Key, pair.Value, null))
            .ToList();

        added.Sort(CompareByPath);
        removed.Sort(CompareByPath);
        changed.Sort(CompareByPath);

        return new TorrentDiff(added, removed, changed);
    }

    private static Dictionary<string, long> ToLengthMap(IEnumerable<TorrentFileEntry> files)
    {
        // A malformed torrent can repeat a path; the first entry wins instead of
        // throwing, so a bad file cannot take the whole comparison down.
        var map = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            map.TryAdd(file.RelativePath, file.Length);
        }

        return map;
    }

    private static int CompareByPath(TorrentFileChange left, TorrentFileChange right)
    {
        return string.CompareOrdinal(left.Path, right.Path);
    }
}
