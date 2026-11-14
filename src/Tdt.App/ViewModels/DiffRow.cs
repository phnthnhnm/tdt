using System.Globalization;
using Tdt.Core.Models;

namespace Tdt.App.ViewModels;

/// <summary>Why a file appears in the comparison list.</summary>
public enum DiffRowKind
{
    Added,
    Removed,
    Changed,
}

/// <summary>One row of the comparison list, formatted for display.</summary>
public sealed class DiffRow
{
    public DiffRow(TorrentFileChange change, DiffRowKind kind)
    {
        ArgumentNullException.ThrowIfNull(change);
        Path = change.Path;
        Kind = kind;

        var previous = SizeFormatter.Format(change.PreviousLength ?? 0);
        var current = SizeFormatter.Format(change.NewLength ?? 0);

        SizeText = kind switch
        {
            DiffRowKind.Added => current,
            DiffRowKind.Removed => previous,
            _ => string.Create(CultureInfo.InvariantCulture, $"{previous} to {current}"),
        };

        // For an addition or a removal the size already is the delta; only a
        // replacement needs both numbers.
        var delta = change.SizeDelta;
        DeltaText = kind == DiffRowKind.Changed && delta != 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{(delta > 0 ? "+" : string.Empty)}{SizeFormatter.Format(delta)}")
            : string.Empty;
    }

    public string Path { get; }

    public DiffRowKind Kind { get; }

    public bool IsAdded => Kind == DiffRowKind.Added;

    public bool IsRemoved => Kind == DiffRowKind.Removed;

    public bool IsChanged => Kind == DiffRowKind.Changed;

    /// <summary>Segoe Fluent Icons glyph for the row.</summary>
    public string Glyph => Kind switch
    {
        DiffRowKind.Added => "\uE710",
        DiffRowKind.Removed => "\uE738",
        _ => "\uE70F",
    };

    public string SizeText { get; }

    public string DeltaText { get; }

    /// <summary>What a screen reader announces for this row.</summary>
    public string AutomationName => Kind switch
    {
        DiffRowKind.Added => $"Added, {Path}, {SizeText}",
        DiffRowKind.Removed => $"Removed, {Path}, {SizeText}",
        _ => $"Changed, {Path}, {SizeText}",
    };
}

/// <summary>Byte sizes in the largest sensible unit.</summary>
public static class SizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Format(long bytes)
    {
        var sign = bytes < 0 ? "-" : string.Empty;
        double value = Math.Abs(bytes);
        var unit = 0;

        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var format = unit == 0 || value >= 100 ? "0" : "0.#";
        var number = value.ToString(format, CultureInfo.CurrentCulture);
        return string.Create(CultureInfo.CurrentCulture, $"{sign}{number} {Units[unit]}");
    }
}
