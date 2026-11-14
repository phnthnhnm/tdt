using Tdt.Core.Models;
using Tdt.Core.Torrent;

namespace Tdt.Core.Services;

public interface ITorrentService
{
    /// <summary>Reads and parses a torrent file.</summary>
    /// <exception cref="TorrentFormatException">The file is not a usable torrent.</exception>
    Task<TorrentInfo> LoadAsync(string torrentPath, CancellationToken cancellationToken = default);
}

public sealed class TorrentService : ITorrentService
{
    public async Task<TorrentInfo> LoadAsync(
        string torrentPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(torrentPath);

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(torrentPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new TorrentFormatException(
                $"Could not read '{Path.GetFileName(torrentPath)}': {ex.Message}",
                ex);
        }

        return TorrentParser.Parse(bytes);
    }
}
