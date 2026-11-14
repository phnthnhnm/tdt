namespace Tdt.Core.Models;

/// <summary>Everything the app persists between runs.</summary>
public sealed class AppSettings
{
    /// <summary>Schema version, so a future change can migrate instead of guessing.</summary>
    public int Version { get; set; } = 1;

    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>Folder holding the previously downloaded torrent files.</summary>
    public string DataFolder { get; set; } = string.Empty;

    public QBittorrentSettings QBittorrent { get; set; } = new();

    public bool HasDataFolder => !string.IsNullOrWhiteSpace(DataFolder);
}
