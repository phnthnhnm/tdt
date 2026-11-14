using Tdt.Core.Models;
using Tdt.Core.Services;
using Tdt.Core.Tests.Support;

namespace Tdt.Core.Tests;

public sealed class TorrentDiffTests
{
    [Fact]
    public void SortsAddedAndRemovedPaths()
    {
        var diff = TorrentDiff.Between(
            ["b.cbz", "a.cbz", "gone.cbz"],
            ["d.cbz", "c.cbz", "b.cbz"]);

        Assert.Equal(["c.cbz", "d.cbz"], diff.Added);
        Assert.Equal(["a.cbz", "gone.cbz"], diff.Removed);
        Assert.False(diff.IsEmpty);
    }

    [Fact]
    public void ReportsNoChangesForEqualSets()
    {
        var diff = TorrentDiff.Between(["a.cbz", "b.cbz"], ["b.cbz", "a.cbz"]);

        Assert.Empty(diff.Added);
        Assert.Empty(diff.Removed);
        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void ReportsOnlyAdditions()
    {
        var diff = TorrentDiff.Between(["a.cbz"], ["a.cbz", "b.cbz"]);

        Assert.Equal(["b.cbz"], diff.Added);
        Assert.Empty(diff.Removed);
    }

    [Fact]
    public void ReportsOnlyRemovals()
    {
        var diff = TorrentDiff.Between(["a.cbz", "b.cbz"], ["a.cbz"]);

        Assert.Empty(diff.Added);
        Assert.Equal(["b.cbz"], diff.Removed);
    }

    [Fact]
    public void TreatsARenamedTorrentRootAsAFullReplacement()
    {
        var before = BencodeFixture.TorrentFilePath("v1", "001.cbz");
        var after = BencodeFixture.TorrentFilePath("v2", "001.cbz");

        var diff = TorrentDiff.Between([before], [after]);

        Assert.Equal([after], diff.Added);
        Assert.Equal([before], diff.Removed);
    }

    [Fact]
    public void ComparesPathsCaseSensitively()
    {
        var diff = TorrentDiff.Between(["001.CBZ"], ["001.cbz"]);

        Assert.Single(diff.Added);
        Assert.Single(diff.Removed);
    }

    [Fact]
    public void IgnoresDuplicateEntries()
    {
        var diff = TorrentDiff.Between(["a.cbz", "a.cbz"], ["a.cbz"]);

        Assert.True(diff.IsEmpty);
    }
}

public sealed class BlacklistTests
{
    [Fact]
    public void NormalizesEntries()
    {
        var normalized = Blacklist.Normalize(["  Sample ", "sample", "", "   ", "XXX"]);

        Assert.Equal(["sample", "xxx"], normalized);
    }

    [Fact]
    public void MatchesSubstringsCaseInsensitively()
    {
        var entries = Blacklist.Normalize(["sample"]);

        Assert.True(Blacklist.Matches(Path.Combine("Release", "sample", "001.cbz"), entries));
        Assert.True(Blacklist.Matches(Path.Combine("Release", "SAMPLE", "001.cbz"), entries));
        Assert.False(Blacklist.Matches(Path.Combine("Release", "main", "001.cbz"), entries));
    }

    [Fact]
    public void IgnoresEmptyEntries()
    {
        Assert.False(Blacklist.Matches("001.cbz", [string.Empty, ""]));
    }

    [Fact]
    public void SelectsTheMatchingFiles()
    {
        var entries = Blacklist.Normalize(["sample"]);

        var matches = Blacklist.SelectMatching(
            [Path.Combine("R", "sample", "a.cbz"), Path.Combine("R", "b.cbz")],
            entries);

        Assert.Single(matches);
        Assert.Contains("sample", matches[0], StringComparison.Ordinal);
    }
}

public sealed class TorrentPathsTests
{
    [Fact]
    public void NormalizesForwardSlashes()
    {
        Assert.Equal(
            Path.Combine("Release", "sub", "001.cbz"),
            TorrentPaths.Normalize("Release/sub/001.cbz"));
    }

    [Theory]
    [InlineData("Release/sub/001.cbz", "Release/001.cbz", true)]
    [InlineData("Release/sub/001.cbz", "Other/001.cbz", true)]
    [InlineData("Release/sub/001.cbz", "Release/sub/002.cbz", false)]
    [InlineData("Release/001.cbz", "Release/001.CBZ", false)]
    public void ComparesByFileName(string candidate, string target, bool expected)
    {
        Assert.Equal(expected, TorrentPaths.IsSameFile(candidate, target));
    }

    [Fact]
    public void HandlesBackslashPaths()
    {
        var target = string.Join(Path.DirectorySeparatorChar, "Release", "001.cbz");

        Assert.True(TorrentPaths.IsSameFile("Release/001.cbz", target));
    }
}
