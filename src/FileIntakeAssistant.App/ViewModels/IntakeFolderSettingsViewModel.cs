using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using FileIntakeAssistant.Core.Configuration;
using FileIntakeAssistant.Core.Models;

namespace FileIntakeAssistant.App.ViewModels;

public sealed class IntakeFolderSettingsViewModel : INotifyPropertyChanged
{
    private readonly IntakeFolderSettingsService _settingsService;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<DateTimeOffset, IntakeFolder?> _defaultSuggestionFactory;
    private readonly Func<string, bool> _directoryExists;
    private readonly Func<string, bool> _containsRepositoryMarker;

    private IntakeFolderItemViewModel? _selectedFolder;
    private string _newFolderPath = string.Empty;
    private string _newDisplayName = string.Empty;
    private bool _newRecursive;
    private string _statusMessage = "Ready";
    private bool _isBusy;

    public IntakeFolderSettingsViewModel(
        IntakeFolderSettingsService settingsService,
        Func<DateTimeOffset, IntakeFolder?> defaultSuggestionFactory,
        Func<string, bool>? directoryExists = null,
        Func<string, bool>? containsRepositoryMarker = null,
        Func<DateTimeOffset>? clock = null)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _defaultSuggestionFactory = defaultSuggestionFactory ?? throw new ArgumentNullException(nameof(defaultSuggestionFactory));
        _directoryExists = directoryExists ?? (path => Directory.Exists(path));
        _containsRepositoryMarker = containsRepositoryMarker ?? (path => Directory.Exists(Path.Combine(path, ".git")));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);

        RefreshCommand = new AsyncCommand(() => RefreshAsync(), () => !IsBusy);
        AddCommand = new AsyncCommand(() => AddAsync(), () => !IsBusy);
        EnableCommand = new AsyncCommand(() => EnableSelectedAsync(), () => !IsBusy && SelectedFolder is not null);
        DisableCommand = new AsyncCommand(() => DisableSelectedAsync(), () => !IsBusy && SelectedFolder?.Id is not null);
        RemoveCommand = new AsyncCommand(() => RemoveSelectedAsync(), () => !IsBusy && SelectedFolder?.Id is not null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? FoldersChanged;

    public ObservableCollection<IntakeFolderItemViewModel> Folders { get; } = [];

    public AsyncCommand RefreshCommand { get; }

    public AsyncCommand AddCommand { get; }

    public AsyncCommand EnableCommand { get; }

    public AsyncCommand DisableCommand { get; }

    public AsyncCommand RemoveCommand { get; }

    public IntakeFolderItemViewModel? SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (SetField(ref _selectedFolder, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string NewFolderPath
    {
        get => _newFolderPath;
        set => SetField(ref _newFolderPath, value);
    }

    public string NewDisplayName
    {
        get => _newDisplayName;
        set => SetField(ref _newDisplayName, value);
    }

    public bool NewRecursive
    {
        get => _newRecursive;
        set => SetField(ref _newRecursive, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            await LoadFoldersAsync(cancellationToken).ConfigureAwait(true);
            StatusMessage = "Intake folders refreshed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AddAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(NewFolderPath))
        {
            StatusMessage = "Enter an intake folder path.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _settingsService.AddOrUpdateAsync(
                new IntakeFolderSettingsRequest(
                    NewFolderPath,
                    NewDisplayName,
                    "Intake",
                    Enabled: true,
                    NewRecursive,
                    DirectoryExistsSafe(NewFolderPath),
                    ContainsRepositoryMarkerSafe(NewFolderPath)),
                _clock(),
                cancellationToken).ConfigureAwait(true);

            StatusMessage = result.Message;
            if (result.Succeeded)
            {
                NewFolderPath = string.Empty;
                NewDisplayName = string.Empty;
                NewRecursive = false;
                await LoadFoldersAndNotifyAsync(cancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task EnableSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedFolder is null)
        {
            StatusMessage = "Select an intake folder first.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _settingsService.EnableAsync(
                SelectedFolder.Folder,
                _clock(),
                DirectoryExistsSafe(SelectedFolder.Path),
                ContainsRepositoryMarkerSafe(SelectedFolder.Path),
                cancellationToken).ConfigureAwait(true);

            StatusMessage = result.Message;
            if (result.Succeeded)
            {
                await LoadFoldersAndNotifyAsync(cancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task DisableSelectedAsync(CancellationToken cancellationToken = default)
    {
        await SetSelectedInactiveAsync(remove: false, cancellationToken).ConfigureAwait(true);
    }

    public async Task RemoveSelectedAsync(CancellationToken cancellationToken = default)
    {
        await SetSelectedInactiveAsync(remove: true, cancellationToken).ConfigureAwait(true);
    }

    private async Task SetSelectedInactiveAsync(bool remove, CancellationToken cancellationToken)
    {
        if (SelectedFolder?.Id is not long id)
        {
            StatusMessage = "Select a saved intake folder first.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = remove
                ? await _settingsService.RemoveFromWatchListAsync(id, _clock(), cancellationToken).ConfigureAwait(true)
                : await _settingsService.DisableAsync(id, _clock(), cancellationToken).ConfigureAwait(true);

            StatusMessage = result.Message;
            if (result.Succeeded)
            {
                await LoadFoldersAndNotifyAsync(cancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadFoldersAndNotifyAsync(CancellationToken cancellationToken)
    {
        await LoadFoldersAsync(cancellationToken).ConfigureAwait(true);
        FoldersChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task LoadFoldersAsync(CancellationToken cancellationToken)
    {
        var folders = await _settingsService.ListWithSuggestionAsync(
            _defaultSuggestionFactory(_clock()),
            cancellationToken).ConfigureAwait(true);

        Folders.Clear();
        foreach (var folder in folders)
        {
            Folders.Add(new IntakeFolderItemViewModel(folder));
        }

        SelectedFolder = Folders.FirstOrDefault(folder => SelectedFolder?.Id == folder.Id)
            ?? Folders.FirstOrDefault();
    }

    private bool DirectoryExistsSafe(string path)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(path) && _directoryExists(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private bool ContainsRepositoryMarkerSafe(string path)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(path) && _containsRepositoryMarker(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private void RaiseCommandStates()
    {
        RefreshCommand.RaiseCanExecuteChanged();
        AddCommand.RaiseCanExecuteChanged();
        EnableCommand.RaiseCanExecuteChanged();
        DisableCommand.RaiseCanExecuteChanged();
        RemoveCommand.RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
