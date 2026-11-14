namespace Tdt.Core.Services;

public interface IQBittorrentClient
{
    /// <summary>True when requests authenticate with an API key instead of a cookie.</summary>
    bool HasApiKey { get; }

    /// <summary>
    /// Sets the qBittorrent 5.2+ API key. An empty value falls back to form
    /// login.
    /// </summary>
    void SetApiKey(string? apiKey);

    /// <summary>Form login; captures the session cookie for later requests.</summary>
    /// <exception cref="InvalidOperationException">An API key is configured.</exception>
    Task LoginAsync(
        QBittorrentConnection connection,
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task AddTorrentAsync(
        QBittorrentConnection connection,
        byte[] torrentBytes,
        string? savePath = null,
        bool paused = true,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QBittorrentTorrent>> GetTorrentsAsync(
        QBittorrentConnection connection,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QBittorrentFileEntry>> GetFilesAsync(
        QBittorrentConnection connection,
        string hash,
        CancellationToken cancellationToken = default);

    Task SetFilePriorityAsync(
        QBittorrentConnection connection,
        string hash,
        IReadOnlyCollection<int> fileIndexes,
        int priority,
        CancellationToken cancellationToken = default);

    Task StopAsync(
        QBittorrentConnection connection,
        string hashes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads <c>app/preferences</c> and remembers <c>bypass_local_auth</c>, so
    /// later requests stop sending a cookie when the server does not need one.
    /// </summary>
    Task<bool> GetBypassLocalAuthAsync(
        QBittorrentConnection connection,
        CancellationToken cancellationToken = default);
}
