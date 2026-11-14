using System.Text.Json.Serialization;

namespace Tdt.Core.Services;

/// <summary>Where a qBittorrent WebUI instance lives.</summary>
public sealed record QBittorrentConnection(string Host, int Port, bool UseHttps)
{
    public string BaseUrl => string.Create(
        System.Globalization.CultureInfo.InvariantCulture,
        $"{(UseHttps ? "https" : "http")}://{Host}:{Port}");
}

/// <summary>A torrent as reported by <c>torrents/info</c>.</summary>
public sealed class QBittorrentTorrent
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("hash")]
    public string Hash { get; init; } = string.Empty;
}

/// <summary>A file row as reported by <c>torrents/files</c>.</summary>
public sealed class QBittorrentFileEntry
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

/// <summary>A non-success answer from the qBittorrent WebUI API.</summary>
public sealed class QBittorrentException : Exception
{
    public QBittorrentException(string message)
        : base(message)
    {
    }

    public QBittorrentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
