using Tdt.Core.Tests.Support;
using Tdt.Core.Torrent;

namespace Tdt.Core.Tests;

public sealed class TorrentParserTests
{
    [Fact]
    public void PrefixesEveryPathWithTheTorrentName()
    {
        var torrent = TorrentParser.Parse(BencodeFixture.MultiFileTorrent(
            "Release",
            ["001.cbz"],
            ["extras", "002.cbz"]));

        Assert.Equal("Release", torrent.Name);
        Assert.Equal(
            [
                BencodeFixture.TorrentFilePath("Release", "001.cbz"),
                BencodeFixture.TorrentFilePath("Release", "extras", "002.cbz"),
            ],
            torrent.Files.Select(file => file.Path));
        Assert.Equal(2048, torrent.TotalLength);
    }

    [Fact]
    public void UsesTheTorrentNameForASingleFileTorrent()
    {
        var torrent = TorrentParser.Parse(
            BencodeFixture.SingleFileTorrent("chapter-01.cbz", length: 99));

        var file = Assert.Single(torrent.Files);
        Assert.Equal("chapter-01.cbz", file.Path);
        Assert.Equal(99, file.Length);
        Assert.Equal(99, torrent.TotalLength);
    }

    [Fact]
    public void PrefersUtf8Name()
    {
        var data = BencodeFixture.Encode(new Dictionary<string, object?>
        {
            ["info"] = new Dictionary<string, object?>
            {
                ["name"] = "fallback",
                ["name.utf-8"] = "preferred",
                ["piece length"] = 16384,
                ["pieces"] = BencodeFixture.PieceHashes(),
                ["length"] = 10,
            },
        });

        var torrent = TorrentParser.Parse(data);

        Assert.Equal("preferred", torrent.Name);
    }

    [Fact]
    public void PrefersUtf8PathSegments()
    {
        var data = BencodeFixture.Encode(new Dictionary<string, object?>
        {
            ["info"] = new Dictionary<string, object?>
            {
                ["name"] = "Release",
                ["piece length"] = 16384,
                ["pieces"] = BencodeFixture.PieceHashes(),
                ["files"] = new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["length"] = 1,
                        ["path"] = new List<object?> { "plain" },
                        ["path.utf-8"] = new List<object?> { "ünïcode" },
                    },
                },
            },
        });

        var torrent = TorrentParser.Parse(data);

        Assert.Equal(
            BencodeFixture.TorrentFilePath("Release", "ünïcode"),
            torrent.Files[0].Path);
    }

    [Fact]
    public void TreatsAnEmptyFileListAsTheTorrentName()
    {
        var data = BencodeFixture.Encode(new Dictionary<string, object?>
        {
            ["info"] = new Dictionary<string, object?>
            {
                ["name"] = "Release",
                ["piece length"] = 16384,
                ["pieces"] = BencodeFixture.PieceHashes(),
                ["files"] = new List<object?>(),
            },
        });

        var torrent = TorrentParser.Parse(data);

        var file = Assert.Single(torrent.Files);
        Assert.Equal("Release", file.Path);
    }

    [Fact]
    public void RejectsAMissingInfoDictionary()
    {
        var data = BencodeFixture.Encode(new Dictionary<string, object?>
        {
            ["announce"] = "http://tracker.example/announce",
        });

        var error = Assert.Throws<TorrentFormatException>(() => TorrentParser.Parse(data));

        Assert.Contains("info", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAMissingName()
    {
        var data = BencodeFixture.Encode(new Dictionary<string, object?>
        {
            ["info"] = new Dictionary<string, object?>
            {
                ["length"] = 10,
            },
        });

        var error = Assert.Throws<TorrentFormatException>(() => TorrentParser.Parse(data));

        Assert.Contains("info.name", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAFileWithoutLength()
    {
        var data = BencodeFixture.Encode(new Dictionary<string, object?>
        {
            ["info"] = new Dictionary<string, object?>
            {
                ["name"] = "Release",
                ["files"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["path"] = new List<object?> { "001.cbz" } },
                },
            },
        });

        var error = Assert.Throws<TorrentFormatException>(() => TorrentParser.Parse(data));

        Assert.Contains("info.files[0].length", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAFileWithoutPath()
    {
        var data = BencodeFixture.Encode(new Dictionary<string, object?>
        {
            ["info"] = new Dictionary<string, object?>
            {
                ["name"] = "Release",
                ["files"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["length"] = 1 },
                },
            },
        });

        var error = Assert.Throws<TorrentFormatException>(() => TorrentParser.Parse(data));

        Assert.Contains("info.files[0].path", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnEmptyFile()
    {
        var error = Assert.Throws<TorrentFormatException>(() => TorrentParser.Parse([]));

        Assert.Contains("empty", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
