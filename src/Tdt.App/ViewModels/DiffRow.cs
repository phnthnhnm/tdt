namespace Tdt.App.ViewModels;

/// <summary>One row of the comparison list.</summary>
public sealed class DiffRow
{
    public DiffRow(string path, bool isAdded)
    {
        Path = path;
        IsAdded = isAdded;
    }

    public string Path { get; }

    /// <summary>True for a file the new torrent adds.</summary>
    public bool IsAdded { get; }
}
