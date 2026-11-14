using Tdt.Core.Models;
using Tdt.Core.Services;
using Tdt.Core.Tests.Support;
using Tdt.Core.Torrent;

namespace Tdt.Core.Tests;

public sealed class TorrentDiffTests
{
    private static TorrentFileEntry File(string path, long length) => new(path, length);

    [Fact]
    public void ReportsAddedRemovedAndChangedFiles()
    {
        var archived = new[]
        {
            File(BencodeFixture.Relative("001.cbz"), 100),
            File(BencodeFixture.Relative("002.cbz"), 200),
            File(BencodeFixture.Relative("old.cbz"), 300),
        };
        var released = new[]
        {
            File(BencodeFixture.Relative("001.cbz"), 100),
            File(BencodeFixture.Relative("002.cbz"), 999),
            File(BencodeFixture.Relative("new.cbz"), 400),
        };

        var diff = TorrentDiff.Between(archived, released);

        var added = Assert.Single(diff.Added);
        Assert.Equal(BencodeFixture.Relative("new.cbz"), added.Path);
        Assert.Null(added.PreviousLength);
        Assert.Equal(400, added.NewLength);

        var removed = Assert.Single(diff.Removed);
        Assert.Equal(BencodeFixture.Relative("old.cbz"), removed.Path);
        Assert.Equal(-300, removed.SizeDelta);

        var changed = Assert.Single(diff.Changed);
        Assert.Equal(BencodeFixture.Relative("002.cbz"), changed.Path);
        Assert.Equal(200, changed.PreviousLength);
        Assert.Equal(999, changed.NewLength);
        Assert.Equal(799, changed.SizeDelta);

        Assert.Equal(3, diff.Count);
        Assert.False(diff.IsEmpty);
    }

    [Fact]
    public void SortsEveryListByPath()
    {
        var archived = new[]
        {
            File("c.cbz", 1),
            File("a.cbz", 1),
            File("b.cbz", 1),
        };
        var released = new[]
        {
            File("z.cbz", 1),
            File("y.cbz", 1),
            File("x.cbz", 1),
        };

        var diff = TorrentDiff.Between(archived, released);

        Assert.Equal(["a.cbz", "b.cbz", "c.cbz"], diff.Removed.Select(change => change.Path));
        Assert.Equal(["x.cbz", "y.cbz", "z.cbz"], diff.Added.Select(change => change.Path));
    }

    [Fact]
    public void ReportsNoChangesForIdenticalLists()
    {
        var files = new[] { File("a.cbz", 1), File("b.cbz", 2) };

        var diff = TorrentDiff.Between(files, files);

        Assert.True(diff.IsEmpty);
        Assert.Equal(0, diff.Count);
        Assert.Same(TorrentDiff.Empty.GetType(), diff.GetType());
    }

    [Fact]
    public void IgnoresARenamedTorrentRoot()
    {
        var (oldBytes, _) = BencodeFixture.MultiFileTorrent(
            "Release.v1", new BencodeFixture.FileSpec(["001.cbz"], 100));
        var (newBytes, _) = BencodeFixture.MultiFileTorrent(
            "Release.v2", new BencodeFixture.FileSpec(["001.cbz"], 100));

        var diff = TorrentDiff.Between(
            TorrentParser.Parse(oldBytes).Files,
            TorrentParser.Parse(newBytes).Files);

        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void ToDownloadCombinesAdditionsAndReplacements()
    {
        var archived = new[] { File("keep.cbz", 1), File("replaced.cbz", 10) };
        var released = new[] { File("keep.cbz", 1), File("replaced.cbz", 20), File("fresh.cbz", 30) };

        var diff = TorrentDiff.Between(archived, released);

        Assert.Equal(
            ["fresh.cbz", "replaced.cbz"],
            diff.ToDownload.Select(change => change.Path));
    }

    [Fact]
    public void ComparesPathsCaseSensitively()
    {
        var diff = TorrentDiff.Between([File("001.CBZ", 1)], [File("001.cbz", 1)]);

        Assert.Single(diff.Added);
        Assert.Single(diff.Removed);
        Assert.Empty(diff.Changed);
    }

    [Fact]
    public void KeepsTheFirstEntryForADuplicatePath()
    {
        var diff = TorrentDiff.Between(
            [File("001.cbz", 10), File("001.cbz", 20)],
            [File("001.cbz", 10)]);

        Assert.True(diff.IsEmpty);
    }
}

public sealed class TorrentPathsTests
{
    [Theory]
    [InlineData("Release/001.cbz", true)]
    [InlineData("Release/sub/001.cbz", false)]
    [InlineData("Release\\001.cbz", true)]
    [InlineData("release/001.cbz", true)]
    [InlineData("001.cbz", true)]
    public void MatchesRootPrefixedContentPaths(string contentPath, bool expected)
    {
        Assert.Equal(
            expected,
            TorrentPaths.MatchesContentPath(contentPath, "Release", "001.cbz"));
    }

    [Fact]
    public void DistinguishesSiblingFolders()
    {
        var relative = BencodeFixture.Relative("CD2", "001.cbz");

        Assert.False(TorrentPaths.MatchesContentPath("Release/CD1/001.cbz", "Release", relative));
        Assert.True(TorrentPaths.MatchesContentPath("Release/CD2/001.cbz", "Release", relative));
    }

    [Fact]
    public void MatchesNestedRelativePaths()
    {
        var relative = BencodeFixture.Relative("extras", "002.cbz");

        Assert.True(TorrentPaths.MatchesContentPath("Release/extras/002.cbz", "Release", relative));
    }

    [Fact]
    public void NeverMatchesAnEmptyRelativePath()
    {
        Assert.False(TorrentPaths.MatchesContentPath("Release/001.cbz", "Release", string.Empty));
    }

    [Fact]
    public void ToContentPathUsesForwardSlashes()
    {
        Assert.Equal("a/b/c.cbz", TorrentPaths.ToContentPath(BencodeFixture.Relative("a", "b", "c.cbz")));
    }
}
