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
/// Drives the compare workflow: pick a torrent, diff it against the archived
/// copy of the same name, then open only the new files in qBittorrent.
/// </summary>
public sealed partial class DiffViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly IDialogService _dialogs;
    private readonly Window _window;

    private List<string> _added = [];
    private List<string> _removed = [];

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
    private string? _newTorrentPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _statusMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCompare))]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHint))]
    [NotifyPropertyChangedFor(nameof(ShowResults))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyResult))]
    [NotifyPropertyChangedFor(nameof(AddedSummary))]
    [NotifyPropertyChangedFor(nameof(RemovedSummary))]
    private bool _hasCompared;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAdded))]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyResult))]
    [NotifyPropertyChangedFor(nameof(AddedSummary))]
    private int _addedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RemovedSummary))]
    private int _removedCount;

    public bool HasNewTorrent => !string.IsNullOrEmpty(NewTorrentPath);

    public bool CanCompare => HasNewTorrent && !IsBusy;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public bool HasAdded => AddedCount > 0;

    public bool CanAdd => HasAdded && !IsBusy;

    public bool ShowHint => !HasCompared;

    public bool ShowResults => HasCompared;

    public bool ShowEmptyResult => HasCompared && Rows.Count == 0 && AddedCount == 0 && RemovedCount == 0;

    public string AddedSummary => $"New: {AddedCount}";

    public string RemovedSummary => $"Removed: {RemovedCount}";

    [RelayCommand]
    private async Task PickTorrentAsync()
    {
        var path = await _services.Pickers.PickTorrentAsync(_window);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        NewTorrentPath = path;
        ErrorMessage = null;
        StatusMessage = null;
        ValidateOldTorrent(path);
    }

    [RelayCommand]
    private async Task CompareAsync()
    {
        if (string.IsNullOrEmpty(NewTorrentPath))
        {
            return;
        }

        var settings = _services.Settings;
        if (!settings.HasDataFolder)
        {
            ErrorMessage = "Data folder not set in Settings";
            return;
        }

        var oldPath = Path.Combine(settings.DataFolder, Path.GetFileName(NewTorrentPath));
        if (!File.Exists(oldPath))
        {
            ErrorMessage = "No matching old torrent found in data folder";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            var oldTorrent = await _services.Torrents.LoadAsync(oldPath);
            var newTorrent = await _services.Torrents.LoadAsync(NewTorrentPath);
            var diff = TorrentDiff.Between(oldTorrent.Paths, newTorrent.Paths);

            _added = [.. diff.Added];
            _removed = [.. diff.Removed];

            Rows.Clear();
            foreach (var path in _added)
            {
                Rows.Add(new DiffRow(path, isAdded: true));
            }

            foreach (var path in _removed)
            {
                Rows.Add(new DiffRow(path, isAdded: false));
            }

            AddedCount = _added.Count;
            RemovedCount = _removed.Count;
            HasCompared = true;
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
        }
    }

    [RelayCommand]
    private async Task AddToQBittorrentAsync()
    {
        if (_added.Count == 0 || string.IsNullOrEmpty(NewTorrentPath))
        {
            return;
        }

        var qBittorrent = _services.Settings.QBittorrent;
        if (!qBittorrent.IsConfigured)
        {
            ErrorMessage = "qBittorrent settings not configured";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            var connection = new QBittorrentConnection(
                qBittorrent.NormalizedHost,
                qBittorrent.Port,
                qBittorrent.UseHttps);

            _services.QBittorrent.SetApiKey(qBittorrent.HasApiKey ? qBittorrent.ApiKey : null);
            if (!qBittorrent.HasApiKey)
            {
                await _services.QBittorrent.LoginAsync(
                    connection,
                    qBittorrent.Username,
                    qBittorrent.Password);
            }

            var preview = await _services.Adder.PrepareAsync(
                NewTorrentPath,
                _added,
                qBittorrent.Blacklist);

            if (!await _dialogs.ConfirmAddTorrentAsync(preview))
            {
                return;
            }

            await _services.Adder.AddAsync(
                connection,
                NewTorrentPath,
                _added,
                qBittorrent.Blacklist);

            StatusMessage = "Torrent added to qBittorrent (paused)";
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
        }
    }

    private void ValidateOldTorrent(string newPath)
    {
        var settings = _services.Settings;
        if (!settings.HasDataFolder)
        {
            ErrorMessage = "Data folder not set in Settings";
            return;
        }

        var candidate = Path.Combine(settings.DataFolder, Path.GetFileName(newPath));
        if (!File.Exists(candidate))
        {
            ErrorMessage = "No matching old torrent found in data folder";
        }
    }
}
