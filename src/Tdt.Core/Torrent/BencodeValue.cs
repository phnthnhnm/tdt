namespace Tdt.Core.Torrent;

/// <summary>
/// A decoded bencode value. Only the four bencode types exist, so the
/// hierarchy is closed.
/// </summary>
internal abstract record BencodeValue;

internal sealed record BencodeInteger(long Value) : BencodeValue;

internal sealed record BencodeString(byte[] Value) : BencodeValue;

internal sealed record BencodeList(IReadOnlyList<BencodeValue> Items) : BencodeValue;

internal sealed record BencodeDictionary(IReadOnlyDictionary<string, BencodeValue> Entries)
    : BencodeValue;
