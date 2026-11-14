using System.Buffers.Text;

namespace Tdt.Core.Torrent;

/// <summary>
/// A bounds-checked bencode reader. It rejects truncated input, trailing data
/// and pathological nesting instead of trusting the file.
/// </summary>
internal sealed class BencodeReader
{
    private const int MaxDepth = 64;

    private readonly byte[] _data;
    private int _index;

    private BencodeReader(byte[] data)
    {
        _data = data;
    }

    public static BencodeValue Read(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0)
        {
            throw new TorrentFormatException("Torrent file is empty");
        }

        var reader = new BencodeReader(data);
        var value = reader.ReadValue(0);
        if (reader._index != data.Length)
        {
            throw new TorrentFormatException(
                $"Unexpected trailing data at offset {reader._index}");
        }

        return value;
    }

    private BencodeValue ReadValue(int depth)
    {
        if (depth > MaxDepth)
        {
            throw new TorrentFormatException("Torrent data is nested too deeply");
        }

        if (_index >= _data.Length)
        {
            throw new TorrentFormatException("Torrent data ended unexpectedly");
        }

        var start = _index;
        return _data[_index] switch
        {
            (byte)'i' => ReadInteger(start),
            (byte)'l' => ReadList(depth, start),
            (byte)'d' => ReadDictionary(depth, start),
            >= (byte)'0' and <= (byte)'9' => ReadString(start),
            var other => throw new TorrentFormatException(
                $"Unexpected byte 0x{other:x2} at offset {_index}"),
        };
    }

    private BencodeInteger ReadInteger(int start)
    {
        _index++;
        var digitsStart = _index;
        while (_index < _data.Length && _data[_index] != (byte)'e')
        {
            _index++;
        }

        if (_index >= _data.Length)
        {
            throw new TorrentFormatException("Unterminated integer");
        }

        var span = _data.AsSpan(digitsStart, _index - digitsStart);
        _index++;

        if (span.IsEmpty)
        {
            throw new TorrentFormatException("Empty integer");
        }

        if (!Utf8Parser.TryParse(span, out long value, out var consumed) ||
            consumed != span.Length)
        {
            throw new TorrentFormatException("Invalid integer");
        }

        return new BencodeInteger(value, start, _index);
    }

    private BencodeString ReadString(int start)
    {
        var colon = Array.IndexOf(_data, (byte)':', start);
        if (colon < 0)
        {
            throw new TorrentFormatException("Missing ':' in string length");
        }

        var lengthSpan = _data.AsSpan(start, colon - start);
        if (!Utf8Parser.TryParse(lengthSpan, out int length, out var consumed) ||
            consumed != lengthSpan.Length ||
            length < 0)
        {
            throw new TorrentFormatException("Invalid string length");
        }

        var bodyStart = colon + 1;
        if (length > _data.Length - bodyStart)
        {
            throw new TorrentFormatException("String length exceeds the file size");
        }

        var bytes = _data.AsSpan(bodyStart, length).ToArray();
        _index = bodyStart + length;
        return new BencodeString(bytes, start, _index);
    }

    private BencodeList ReadList(int depth, int start)
    {
        _index++;
        var items = new List<BencodeValue>();
        while (true)
        {
            if (_index >= _data.Length)
            {
                throw new TorrentFormatException("Unterminated list");
            }

            if (_data[_index] == (byte)'e')
            {
                _index++;
                return new BencodeList(items, start, _index);
            }

            items.Add(ReadValue(depth + 1));
        }
    }

    private BencodeDictionary ReadDictionary(int depth, int start)
    {
        _index++;
        var entries = new Dictionary<string, BencodeValue>(StringComparer.Ordinal);
        while (true)
        {
            if (_index >= _data.Length)
            {
                throw new TorrentFormatException("Unterminated dictionary");
            }

            if (_data[_index] == (byte)'e')
            {
                _index++;
                return new BencodeDictionary(entries, start, _index);
            }

            if (_data[_index] is < (byte)'0' or > (byte)'9')
            {
                throw new TorrentFormatException("Dictionary keys must be strings");
            }

            var keyStart = _index;
            var key = ReadString(keyStart);
            var value = ReadValue(depth + 1);
            entries[TextDecoder.Decode(key.Value)] = value;
        }
    }
}
