using Tdt.Core.Tests.Support;
using Tdt.Core.Torrent;

namespace Tdt.Core.Tests;

public sealed class TorrentParserTests
{
    [Fact]
    public void ReportsPathsRelativeToTheRoot()
    {
        var (torrent, _) = BencodeFixture.MultiFileTorrent(
            "Release",
            new BencodeFixture.FileSpec(["001.cbz"], 100),
            new BencodeFixture.FileSpec(["extras", "002.cbz"], 200));

        var parsed = TorrentParser.Parse(torrent);

        Assert.Equal("Release", parsed.Name);
        Assert.Equal(
            [BencodeFixture.Relative("001.cbz"), BencodeFixture.Relative("extras", "002.cbz")],
            parsed.Files.Select(file => file.RelativePath));
        Assert.Equal(300, parsed.TotalLength);
    }

    [Fact]
    public void ComputesTheInfoHash()
    {
        var (torrent, info) = BencodeFixture.MultiFileTorrent(
            "Release",
            new BencodeFixture.FileSpec(["001.cbz"], 100));

        var parsed = TorrentParser.Parse(torrent);

        Assert.Equal(BencodeFixture.ExpectedInfoHash(info), parsed.InfoHash);
        Assert.Equal(40, parsed.InfoHash.Length);
        Assert.Equal(parsed.InfoHash.ToLowerInvariant(), parsed.InfoHash);
    }

    [Fact]
    public void UsesTheTorrentNameForASingleFileTorrent()
    {
        var (torrent, _) = BencodeFixture.SingleFileTorrent("chapter-01.cbz", length: 99);

        var parsed = TorrentParser.Parse(torrent);

        var file = Assert.Single(parsed.Files);
        Assert.Equal("chapter-01.cbz", file.RelativePath);
        Assert.Equal(99, file.Length);
        Assert.Equal(99, parsed.TotalLength);
    }

    [Fact]
    public void PrefersUtf8Name()
    {
        var info = new Dictionary<string, object?>
        {
            ["name"] = "fallback",
            ["name.utf-8"] = "preferred",
            ["piece length"] = 16384,
            ["pieces"] = BencodeFixture.PieceHashes(),
            ["length"] = 10,
        };

        var torrent = BencodeFixture.Encode(new Dictionary<string, object?>
        {
            ["info"] = info,
        });

        Assert.Equal("preferred", TorrentParser.Parse(torrent).Name);
    }

    [Fact]
    public void PrefersUtf8PathSegments()
    {
        var torrent = BencodeFixture.Encode(new Dictionary<string, object?>
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
                        ["path.utf-8"] = new List<object?> { "unicode" },
                    },
                },
            },
        });

        var parsed = TorrentParser.Parse(torrent);

        Assert.Equal(BencodeFixture.Relative("unicode"), parsed.Files[0].RelativePath);
    }

    [Fact]
    public void TreatsAnEmptyFileListAsTheTorrentName()
    {
        var torrent = BencodeFixture.Encode(new Dictionary<string, object?>
        {
            ["info"] = new Dictionary<string, object?>
            {
                ["name"] = "Release",
                ["piece length"] = 16384,
                ["pieces"] = BencodeFixture.PieceHashes(),
                ["files"] = new List<object?>(),
            },
        });

        var parsed = TorrentParser.Parse(torrent);

        var file = Assert.Single(parsed.Files);
        Assert.Equal("Release", file.RelativePath);
    }

    [Fact]
    public void RejectsAMissingInfoDictionary()
    {
        var torrent = BencodeFixture.Encode(new Dictionary<string, object?>
        {
            ["announce"] = "http://tracker.example/announce",
        });

        var error = Assert.Throws<TorrentFormatException>(() => TorrentParser.Parse(torrent));

        Assert.Contains("info", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAMissingName()
    {
        var torrent = BencodeFixture.Encode(new Dictionary<string, object?>
        {
            ["info"] = new Dictionary<string, object?> { ["length"] = 10 },
        });

        var error = Assert.Throws<TorrentFormatException>(() => TorrentParser.Parse(torrent));

        Assert.Contains("info.name", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAFileWithoutLength()
    {
        var torrent = BencodeFixture.Encode(new Dictionary<string, object?>
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

        var error = Assert.Throws<TorrentFormatException>(() => TorrentParser.Parse(torrent));

        Assert.Contains("info.files[0].length", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAFileWithoutPath()
    {
        var torrent = BencodeFixture.Encode(new Dictionary<string, object?>
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

        var error = Assert.Throws<TorrentFormatException>(() => TorrentParser.Parse(torrent));

        Assert.Contains("info.files[0].path", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnEmptyFile()
    {
        var error = Assert.Throws<TorrentFormatException>(() => TorrentParser.Parse([]));

        Assert.Contains("empty", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
