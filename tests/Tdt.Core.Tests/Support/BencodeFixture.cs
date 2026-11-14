using System.Text;

namespace Tdt.Core.Tests.Support;

/// <summary>
/// Bencode writer used to build torrent fixtures in memory, so the tests do not
/// depend on checked-in binary files.
/// </summary>
internal static class BencodeFixture
{
    public static byte[] Encode(object? value)
    {
        using var stream = new MemoryStream();
        Write(value, stream);
        return stream.ToArray();
    }

    /// <summary>A multi-file torrent; <paramref name="paths"/> are path segments.</summary>
    public static byte[] MultiFileTorrent(string name, params string[][] paths)
    {
        return Encode(new Dictionary<string, object?>
        {
            ["announce"] = "http://tracker.example/announce",
            ["info"] = new Dictionary<string, object?>
            {
                ["name"] = name,
                ["piece length"] = 16384,
                ["pieces"] = PieceHashes(),
                ["files"] = paths
                    .Select(segments => (object?)new Dictionary<string, object?>
                    {
                        ["length"] = 1024,
                        ["path"] = segments.Cast<object?>().ToList(),
                    })
                    .ToList(),
            },
        });
    }

    /// <summary>A single-file torrent, where info carries its own length.</summary>
    public static byte[] SingleFileTorrent(string name, long length = 4096)
    {
        return Encode(new Dictionary<string, object?>
        {
            ["announce"] = "http://tracker.example/announce",
            ["info"] = new Dictionary<string, object?>
            {
                ["name"] = name,
                ["piece length"] = 16384,
                ["pieces"] = PieceHashes(),
                ["length"] = length,
            },
        });
    }

    public static byte[] PieceHashes()
    {
        return [.. Enumerable.Range(0, 20).Select(index => (byte)index)];
    }

    /// <summary>The path the parser reports for segments inside a torrent.</summary>
    public static string TorrentFilePath(string name, params string[] segments)
    {
        return string.Join(Path.DirectorySeparatorChar, new[] { name }.Concat(segments));
    }

    private static void Write(object? value, Stream stream)
    {
        switch (value)
        {
            case string text:
                WriteString(Encoding.UTF8.GetBytes(text), stream);
                break;
            case byte[] bytes:
                WriteString(bytes, stream);
                break;
            case int number:
                WriteInteger(number, stream);
                break;
            case long number:
                WriteInteger(number, stream);
                break;
            case IDictionary<string, object?> dictionary:
                stream.WriteByte((byte)'d');
                foreach (var pair in dictionary.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    WriteString(Encoding.UTF8.GetBytes(pair.Key), stream);
                    Write(pair.Value, stream);
                }

                stream.WriteByte((byte)'e');
                break;
            case IEnumerable<object?> items:
                stream.WriteByte((byte)'l');
                foreach (var item in items)
                {
                    Write(item, stream);
                }

                stream.WriteByte((byte)'e');
                break;
            default:
                throw new ArgumentException($"cannot bencode {value?.GetType().Name ?? "null"}");
        }
    }

    private static void WriteInteger(long value, Stream stream)
    {
        var bytes = Encoding.ASCII.GetBytes($"i{value}e");
        stream.Write(bytes);
    }

    private static void WriteString(byte[] bytes, Stream stream)
    {
        var header = Encoding.ASCII.GetBytes($"{bytes.Length}:");
        stream.Write(header);
        stream.Write(bytes);
    }
}
