using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Tdt.Core.Models;

namespace Tdt.Core.Torrent;

/// <summary>
/// Reads the parts of a torrent metainfo file this app needs: the torrent name,
/// its info hash and the file list.
/// </summary>
/// <remarks>
/// File paths are stored relative to the torrent root. A torrent whose root
/// directory was renamed between releases therefore still compares cleanly.
/// </remarks>
public static class TorrentParser
{
    [SuppressMessage(
        "Security",
        "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "BitTorrent v1 identifies a torrent by the SHA-1 of its info dictionary.")]
    public static TorrentInfo Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (BencodeReader.Read(data) is not BencodeDictionary root)
        {
            throw new TorrentFormatException("Torrent is missing required field: info");
        }

        if (!root.Entries.TryGetValue("info", out var infoValue) ||
            infoValue is not BencodeDictionary info)
        {
            throw new TorrentFormatException("Torrent is missing required field: info");
        }

        var name = GetText(info, "name.utf-8") ?? GetText(info, "name");
        if (string.IsNullOrEmpty(name))
        {
            throw new TorrentFormatException("Torrent is missing required field: info.name");
        }

        var files = ReadFiles(info, name);
        var infoHash = SHA1.HashData(data.AsSpan(info.Start, info.Length));

        return new TorrentInfo(
            name,
            Convert.ToHexStringLower(infoHash),
            files,
            files.Sum(file => file.Length));
    }

    private static List<TorrentFileEntry> ReadFiles(BencodeDictionary info, string name)
    {
        var files = new List<TorrentFileEntry>();

        if (!info.Entries.TryGetValue("files", out var filesValue))
        {
            var length = GetInteger(info, "length")
                ?? throw new TorrentFormatException("Torrent is missing required field: info.length");
            files.Add(new TorrentFileEntry(name, length));
            return files;
        }

        if (filesValue is not BencodeList fileList)
        {
            throw new TorrentFormatException("Torrent field info.files must be a list");
        }

        // An empty list carries no file information; falling back to the
        // torrent name keeps the old behavior for such files.
        if (fileList.Items.Count == 0)
        {
            files.Add(new TorrentFileEntry(name, 0));
            return files;
        }

        for (var i = 0; i < fileList.Items.Count; i++)
        {
            if (fileList.Items[i] is not BencodeDictionary file)
            {
                throw new TorrentFormatException($"Torrent field info.files[{i}] must be a dictionary");
            }

            var length = GetInteger(file, "length")
                ?? throw new TorrentFormatException(
                    $"Torrent is missing required field: info.files[{i}].length");
            var segments = ReadPathSegments(file, i);
            files.Add(new TorrentFileEntry(BuildRelativePath(segments), length));
        }

        return files;
    }

    private static List<string> ReadPathSegments(BencodeDictionary file, int index)
    {
        var pathValue = Lookup(file, "path.utf-8") ?? Lookup(file, "path");
        if (pathValue is not BencodeList list)
        {
            throw new TorrentFormatException(
                $"Torrent is missing required field: info.files[{index}].path");
        }

        var segments = new List<string>(list.Items.Count);
        foreach (var item in list.Items)
        {
            if (item is not BencodeString segment)
            {
                throw new TorrentFormatException(
                    $"Torrent field info.files[{index}].path must be a list of strings");
            }

            segments.Add(TextDecoder.Decode(segment.Value));
        }

        return segments;
    }

    private static string BuildRelativePath(List<string> segments)
    {
        return segments.Count == 0
            ? string.Empty
            : string.Join(Path.DirectorySeparatorChar, segments);
    }

    private static BencodeValue? Lookup(BencodeDictionary dictionary, string key)
    {
        return dictionary.Entries.TryGetValue(key, out var value) ? value : null;
    }

    private static string? GetText(BencodeDictionary dictionary, string key)
    {
        return Lookup(dictionary, key) is BencodeString text ? TextDecoder.Decode(text.Value) : null;
    }

    private static long? GetInteger(BencodeDictionary dictionary, string key)
    {
        return Lookup(dictionary, key) is BencodeInteger number ? number.Value : null;
    }
}
