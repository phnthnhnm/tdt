using System.Text.Json.Serialization;

namespace Tdt.Core.Models;

/// <summary>Last window size and position, in physical pixels.</summary>
public sealed class WindowPlacement
{
    public int Width { get; set; }

    public int Height { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    /// <summary>
    /// True when the window was maximized. Width and Height are then the size it
    /// restores to, not the maximized rectangle.
    /// </summary>
    public bool Maximized { get; set; }

    /// <summary>False until the window has been positioned at least once.</summary>
    public bool HasBounds => Width > 0 && Height > 0;
}

/// <summary>Everything the app persists between runs.</summary>
public sealed class AppSettings
{
    /// <summary>Schema version this build writes and understands.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Schema version of the file; a newer file is set aside, not overwritten.</summary>
    public int Version { get; set; } = CurrentVersion;

    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>Folder holding the previously downloaded torrent files.</summary>
    public string DataFolder { get; set; } = string.Empty;

    // The camel-case policy would spell this qBittorrent; pin the documented
    // qbittorrent key so hand-edited files and older backups keep working.
    [JsonPropertyName("qbittorrent")]
    public QBittorrentSettings QBittorrent { get; set; } = new();

    /// <summary>Window size and position restored on the next start.</summary>
    public WindowPlacement Window { get; set; } = new();

    public bool HasDataFolder => !string.IsNullOrWhiteSpace(DataFolder);
}
