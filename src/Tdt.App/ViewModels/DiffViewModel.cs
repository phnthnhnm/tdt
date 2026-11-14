using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Tdt.App.Services;
using Tdt.Core.Models;
using Tdt.Core.Services;
using Tdt.Core.Torrent;

namespace Tdt.App.ViewModels;

/// <summary>
/// Drives the compare workflow: find the archived copy of a released torrent,
/// diff the two, then hand the additions and replacements to qBittorrent.
/// </summary>
public sealed partial class DiffViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly IDialogService _dialogs;
    private readonly Window _window;

    private TorrentComparison? _comparison;

    public DiffViewModel(AppServices services, IDialogService dialogs, Window window)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(window);

        _services = services;
        _dialogs = dialogs;
        _window = window;
    }

    public ObservableCollection<DiffRow> Rows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNewTorrent))]
    [NotifyPropertyChangedFor(nameof(CanCompare))]
    [NotifyPropertyChangedFor(nameof(ShowWelcome))]
    private string? _newTorrentPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasArchivedTorrent))]
    private string? _archivedTorrentPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _statusMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProgress))]
    private string? _progressMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCompare))]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    [NotifyPropertyChangedFor(nameof(ShowWelcome))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowResults))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyResult))]
    [NotifyPropertyChangedFor(nameof(ShowWelcome))]
    private bool _hasCompared;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDownloads))]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private int _addedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private int _removedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDownloads))]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private int _changedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDownloads))]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    private string? _downloadSizeText;

    public bool HasNewTorrent => !string.IsNullOrEmpty(NewTorrentPath);

    public bool HasArchivedTorrent => !string.IsNullOrEmpty(ArchivedTorrentPath);

    public bool CanCompare => HasNewTorrent && !IsBusy;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public bool HasProgress => !string.IsNullOrEmpty(ProgressMessage);

    public bool HasDownloads => AddedCount + ChangedCount > 0;

    public bool CanAdd => HasDownloads && !IsBusy;

    public bool ShowWelcome => !HasCompared && !HasNewTorrent && !IsBusy;

    public bool ShowResults => HasCompared;

    public bool ShowEmptyResult => HasCompared && Rows.Count == 0;

    public string Summary => HasCompared
        ? $"New {AddedCount}   Removed {RemovedCount}   Changed {ChangedCount}"
        : string.Empty;

    public string DownloadSummary => HasDownloads
        ? $"{AddedCount + ChangedCount} file(s), {DownloadSizeText} to download"
        : string.Empty;

    [RelayCommand]
    private async Task PickTorrentAsync()
    {
        var path = await _services.Pickers.PickTorrentAsync(_window);
        if (!string.IsNullOrEmpty(path))
        {
            await LoadTorrentAsync(path);
        }
    }

    /// <summary>
    /// Loads a released torrent, finds its archived copy and compares them.
    /// Used by both the file picker and drag and drop.
    /// </summary>
    public async Task LoadTorrentAsync(string releasedTorrentPath)
    {
        if (string.IsNullOrWhiteSpace(releasedTorrentPath))
        {
            return;
        }

        NewTorrentPath = releasedTorrentPath;
        ArchivedTorrentPath = null;
        _comparison = null;
        Rows.Clear();
        AddedCount = 0;
        RemovedCount = 0;
        ChangedCount = 0;
        DownloadSizeText = null;
        HasCompared = false;
        ErrorMessage = null;
        StatusMessage = null;

        IsBusy = true;
        ProgressMessage = "Looking for the archived torrent...";

        try
        {
            var match = await _services.Locator.LocateAsync(
                releasedTorrentPath, _services.Settings.DataFolder);

            if (!match.IsFound)
            {
                ErrorMessage = match.Status switch
                {
                    ArchivedTorrentStatus.DataFolderNotConfigured =>
                        "Data folder not set in Settings",
                    ArchivedTorrentStatus.DataFolderMissing =>
                        "The data folder does not exist. Choose it in Settings.",
                    _ => "No archived torrent found for this release. Put the old .torrent in the data folder.",
                };
                return;
            }

            ProgressMessage = "Comparing torrents...";
            await ApplyComparisonAsync(match.ArchivedPath!, match.Released!);
        }
        catch (TorrentFormatException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            ProgressMessage = null;
        }
    }

    [RelayCommand]
    private async Task CompareAsync()
    {
        if (string.IsNullOrEmpty(NewTorrentPath))
        {
            return;
        }

        await LoadTorrentAsync(NewTorrentPath);
    }

    [RelayCommand]
    private void Clear()
    {
        NewTorrentPath = null;
        ArchivedTorrentPath = null;
        _comparison = null;
        Rows.Clear();
        AddedCount = 0;
        RemovedCount = 0;
        ChangedCount = 0;
        DownloadSizeText = null;
        HasCompared = false;
        ErrorMessage = null;
        StatusMessage = null;
    }

    [RelayCommand]
    private async Task AddToQBittorrentAsync()
    {
        if (_comparison is null || string.IsNullOrEmpty(NewTorrentPath))
        {
            return;
        }

        var settings = _services.Settings.QBittorrent;
        var validation = settings.Validate();
        if (validation is not null)
        {
            ErrorMessage = validation;
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            ProgressMessage = "Signing in to qBittorrent...";
            var connection = await QBittorrentSession.ConnectAsync(_services.QBittorrent, settings);

            var downloads = _comparison.Diff.ToDownload
                .Select(change => change.Path)
                .ToList();

            var plan = _services.Adder.Plan(_comparison.Released, downloads, settings.Blacklist);

            ProgressMessage = null;
            if (!await _dialogs.ConfirmAddTorrentAsync(plan))
            {
                return;
            }

            ProgressMessage = "Adding the torrent and selecting files...";
            var result = await _services.Adder.AddAsync(connection, NewTorrentPath, plan);

            StatusMessage = result.SelectedCount == 0
                ? "Torrent added to qBittorrent (paused); no files matched the selection"
                : $"Torrent added to qBittorrent (paused); {result.SelectedCount} of {result.FileCount} files selected";
        }
        catch (Exception ex) when (
            ex is QBittorrentException
                or TorrentFormatException
                or IOException
                or UnauthorizedAccessException
                or HttpRequestException
                or TaskCanceledException)
        {
            ErrorMessage = $"qBittorrent error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            ProgressMessage = null;
        }
    }

    private async Task ApplyComparisonAsync(string archivedPath, TorrentInfo released)
    {
        var comparison = await _services.Comparisons.CompareAsync(archivedPath, released);
        _comparison = comparison;

        Rows.Clear();
        foreach (var change in comparison.Diff.Added)
        {
            Rows.Add(new DiffRow(change, DiffRowKind.Added));
        }

        foreach (var change in comparison.Diff.Changed)
        {
            Rows.Add(new DiffRow(change, DiffRowKind.Changed));
        }

        foreach (var change in comparison.Diff.Removed)
        {
            Rows.Add(new DiffRow(change, DiffRowKind.Removed));
        }

        AddedCount = comparison.Diff.Added.Count;
        RemovedCount = comparison.Diff.Removed.Count;
        ChangedCount = comparison.Diff.Changed.Count;
        DownloadSizeText = SizeFormatter.Format(
            comparison.Diff.ToDownload.Sum(change => change.NewLength ?? 0));
        ArchivedTorrentPath = archivedPath;
        HasCompared = true;
    }
}
