namespace Tdt.Core.Models;

/// <summary>The qBittorrent WebUI connection and selection settings.</summary>
public sealed class QBittorrentSettings
{
    public const int DefaultPort = 8080;

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = DefaultPort;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>qBittorrent 5.2+ API key. When set, form login is never attempted.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public bool UseHttps { get; set; }

    /// <summary>Case-insensitive substrings; matching files are never selected.</summary>
    public List<string> Blacklist { get; set; } = [];

    public string NormalizedHost => Host.Trim();

    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(NormalizedHost);

    /// <summary>
    /// Why the settings cannot be used, or null when they are complete.
    /// </summary>
    public string? Validate()
    {
        if (!IsConfigured)
        {
            return "Enter the host of the qBittorrent WebUI.";
        }

        if (Port is < 1 or > 65535)
        {
            return "The port must be a number between 1 and 65535.";
        }

        return null;
    }
}
