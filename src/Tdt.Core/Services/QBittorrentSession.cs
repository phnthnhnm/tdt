using Tdt.Core.Models;

namespace Tdt.Core.Services;

/// <summary>
/// The authentication dance shared by "add to qBittorrent" and the settings
/// page's "Test connection": API key first, form login otherwise.
/// </summary>
public static class QBittorrentSession
{
    /// <summary>
    /// Configures the client for the settings and performs a form login when no
    /// API key is set.
    /// </summary>
    /// <exception cref="QBittorrentException">The settings are invalid or the login was rejected.</exception>
    public static async Task<QBittorrentConnection> ConnectAsync(
        IQBittorrentClient client,
        QBittorrentSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(settings);

        var error = settings.Validate();
        if (error is not null)
        {
            throw new QBittorrentException(error);
        }

        var connection = new QBittorrentConnection(
            settings.NormalizedHost,
            settings.Port,
            settings.UseHttps);

        client.SetApiKey(settings.HasApiKey ? settings.ApiKey : null);

        if (!settings.HasApiKey)
        {
            await client.LoginAsync(
                connection, settings.Username, settings.Password, cancellationToken)
                .ConfigureAwait(false);

            // Optional: learning that the server bypasses local auth only stops
            // the client from sending a cookie. A server that does not expose
            // the endpoint must not make the connection fail.
            try
            {
                await client.GetBypassLocalAuthAsync(connection, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (QBittorrentException)
            {
            }
        }

        return connection;
    }

    /// <summary>
    /// Connects and makes one authenticated read, which is what proves the
    /// credentials work.
    /// </summary>
    public static async Task<int> TestAsync(
        IQBittorrentClient client,
        QBittorrentSettings settings,
        CancellationToken cancellationToken = default)
    {
        var connection = await ConnectAsync(client, settings, cancellationToken)
            .ConfigureAwait(false);

        var torrents = await client.GetTorrentsAsync(connection, cancellationToken)
            .ConfigureAwait(false);

        return torrents.Count;
    }
}
