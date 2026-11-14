namespace Tdt.Core.Services;

/// <summary>
/// The qBittorrent blacklist: case-insensitive substrings that keep matching
/// files out of the selection.
/// </summary>
public static class Blacklist
{
    /// <summary>
    /// Trims, lower-cases, drops empty entries, and removes duplicates while
    /// keeping the order the user entered.
    /// </summary>
    public static List<string> Normalize(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var normalized = new List<string>();
        foreach (var value in values)
        {
            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var lowered = trimmed.ToLowerInvariant();
            if (!normalized.Contains(lowered, StringComparer.Ordinal))
            {
                normalized.Add(lowered);
            }
        }

        return normalized;
    }

    /// <summary>True when <paramref name="path"/> contains any blacklisted substring.</summary>
    public static bool Matches(string path, IReadOnlyList<string> entries)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(entries);

        foreach (var entry in entries)
        {
            if (entry.Length > 0 && path.Contains(entry, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The files of a diff that the blacklist would keep out.</summary>
    public static List<string> SelectMatching(IEnumerable<string> paths, IReadOnlyList<string> entries)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return paths.Where(path => Matches(path, entries)).ToList();
    }
}
