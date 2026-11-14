namespace Tdt.Core.Torrent;

/// <summary>
/// Thrown when a <c>.torrent</c> file cannot be read as a valid metainfo file.
/// </summary>
public sealed class TorrentFormatException : Exception
{
    public TorrentFormatException(string message)
        : base(message)
    {
    }

    public TorrentFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
