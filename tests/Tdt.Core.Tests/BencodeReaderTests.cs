using System.Text;
using Tdt.Core.Torrent;

namespace Tdt.Core.Tests;

public sealed class BencodeReaderTests
{
    private static BencodeValue Read(byte[] data) => BencodeReader.Read(data);

    [Fact]
    public void ReadsNestedDictionariesAndLists()
    {
        var data = Encoding.ASCII.GetBytes("d4:infod4:name4:test6:pieces20:aaaaaaaaaaaaaaaaaaaaee");
        var root = Assert.IsType<BencodeDictionary>(Read(data));
        var info = Assert.IsType<BencodeDictionary>(root.Entries["info"]);

        var name = Assert.IsType<BencodeString>(info.Entries["name"]);
        Assert.Equal("test", Encoding.UTF8.GetString(name.Value));

        var pieces = Assert.IsType<BencodeString>(info.Entries["pieces"]);
        Assert.Equal(20, pieces.Value.Length);
    }

    [Fact]
    public void ReadsNegativeIntegers()
    {
        var value = Assert.IsType<BencodeInteger>(Read(Encoding.ASCII.GetBytes("i-42e")));

        Assert.Equal(-42, value.Value);
    }

    [Fact]
    public void ReadsEmptyContainers()
    {
        var list = Assert.IsType<BencodeList>(Read(Encoding.ASCII.GetBytes("le")));
        Assert.Empty(list.Items);

        var dictionary = Assert.IsType<BencodeDictionary>(Read(Encoding.ASCII.GetBytes("de")));
        Assert.Empty(dictionary.Entries);
    }

    [Fact]
    public void RejectsEmptyInput()
    {
        var error = Assert.Throws<TorrentFormatException>(() => Read([]));

        Assert.Contains("empty", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsTrailingData()
    {
        var error = Assert.Throws<TorrentFormatException>(
            () => Read(Encoding.ASCII.GetBytes("i1ei2e")));

        Assert.Contains("trailing", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsTruncatedString()
    {
        var error = Assert.Throws<TorrentFormatException>(
            () => Read(Encoding.ASCII.GetBytes("5:abc")));

        Assert.Contains("exceeds", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsUnterminatedDictionary()
    {
        var error = Assert.Throws<TorrentFormatException>(
            () => Read(Encoding.ASCII.GetBytes("d1:ai1e")));

        Assert.Contains("Unterminated", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsNonStringDictionaryKey()
    {
        var error = Assert.Throws<TorrentFormatException>(
            () => Read(Encoding.ASCII.GetBytes("di1ei2ee")));

        Assert.Contains("keys must be strings", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsInvalidInteger()
    {
        Assert.Throws<TorrentFormatException>(() => Read(Encoding.ASCII.GetBytes("i12x4e")));
    }

    [Fact]
    public void RejectsExcessiveNesting()
    {
        var nested = string.Concat(Enumerable.Repeat("l", 80)) + string.Concat(Enumerable.Repeat("e", 80));

        var error = Assert.Throws<TorrentFormatException>(
            () => Read(Encoding.ASCII.GetBytes(nested)));

        Assert.Contains("nested", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DecodesNonUtf8TextAsLatin1()
    {
        Assert.Equal("naïve", TextDecoder.Decode(Encoding.Latin1.GetBytes("naïve")));
        Assert.Equal("naïve", TextDecoder.Decode(Encoding.UTF8.GetBytes("naïve")));
    }
}
