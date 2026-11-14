namespace Tdt.Core.Torrent;

/// <summary>
/// A decoded bencode value. Only the four bencode types exist, so the
/// hierarchy is closed. <see cref="Start"/> and <see cref="End"/> are the byte
/// range the value occupied in the source document, which lets callers hash a
/// nested value (the torrent info dictionary) without re-encoding it.
/// </summary>
internal abstract class BencodeValue
{
    protected BencodeValue(int start, int end)
    {
        Start = start;
        End = end;
    }

    public int Start { get; }

    public int End { get; }

    public int Length => End - Start;
}

internal sealed class BencodeInteger : BencodeValue
{
    public BencodeInteger(long value, int start, int end)
        : base(start, end)
    {
        Value = value;
    }

    public long Value { get; }
}

internal sealed class BencodeString : BencodeValue
{
    public BencodeString(byte[] value, int start, int end)
        : base(start, end)
    {
        Value = value;
    }

    public byte[] Value { get; }
}

internal sealed class BencodeList : BencodeValue
{
    public BencodeList(IReadOnlyList<BencodeValue> items, int start, int end)
        : base(start, end)
    {
        Items = items;
    }

    public IReadOnlyList<BencodeValue> Items { get; }
}

internal sealed class BencodeDictionary : BencodeValue
{
    public BencodeDictionary(
        IReadOnlyDictionary<string, BencodeValue> entries,
        int start,
        int end)
        : base(start, end)
    {
        Entries = entries;
    }

    public IReadOnlyDictionary<string, BencodeValue> Entries { get; }
}
