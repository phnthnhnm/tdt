using System.Security.Cryptography;
using System.Text;

namespace Tdt.Core.Tests.Support;

/// <summary>
/// Bencode writer used to build torrent fixtures in memory, so the tests do not
/// depend on checked-in binary files. Each builder returns the torrent bytes and
/// the exact info bytes, which lets a test verify the info hash independently.
/// </summary>
internal static class BencodeFixture
{
    /// <summary>A file inside a fixture torrent.</summary>
    public readonly record struct FileSpec(string[] Segments, long Length);

    public static (byte[] Torrent, byte[] Info) MultiFileTorrent(
        string name,
        params FileSpec[] files)
    {
        var info = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["piece length"] = 16384,
            ["pieces"] = PieceHashes(),
            ["files"] = files
                .Select(file => (object?)new Dictionary<string, object?>
                {
                    ["length"] = file.Length,
                    ["path"] = file.Segments.Cast<object?>().ToList(),
                })
                .ToList(),
        };

        return Wrap(info);
    }

    public static (byte[] Torrent, byte[] Info) SingleFileTorrent(
        string name,
        long length = 4096)
    {
        var info = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["piece length"] = 16384,
            ["pieces"] = PieceHashes(),
            ["length"] = length,
        };

        return Wrap(info);
    }

    public static byte[] PieceHashes()
    {
        return [.. Enumerable.Range(0, 20).Select(index => (byte)index)];
    }

    /// <summary>The hash a correct parser must report for these info bytes.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "BitTorrent v1 identifies a torrent by the SHA-1 of its info dictionary.")]
    public static string ExpectedInfoHash(byte[] info)
    {
        return Convert.ToHexStringLower(SHA1.HashData(info));
    }

    /// <summary>A path relative to the torrent root, as the parser reports it.</summary>
    public static string Relative(params string[] segments)
    {
        return string.Join(Path.DirectorySeparatorChar, segments);
    }

    public static byte[] Encode(object? value)
    {
        using var stream = new MemoryStream();
        Write(value, stream);
        return stream.ToArray();
    }

    private static (byte[] Torrent, byte[] Info) Wrap(Dictionary<string, object?> info)
    {
        var infoBytes = Encode(info);
        var torrent = Encode(new Dictionary<string, object?>
        {
            ["announce"] = "http://tracker.example/announce",
            ["info"] = info,
        });

        return (torrent, infoBytes);
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
        stream.Write(Encoding.ASCII.GetBytes($"i{value}e"));
    }

    private static void WriteString(byte[] bytes, Stream stream)
    {
        stream.Write(Encoding.ASCII.GetBytes($"{bytes.Length}:"));
        stream.Write(bytes);
    }
}
