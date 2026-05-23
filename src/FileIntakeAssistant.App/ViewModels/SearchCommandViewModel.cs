using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;
using FileIntakeAssistant.Core.Search;

namespace FileIntakeAssistant.App.ViewModels;

public sealed class SearchCommandViewModel : INotifyPropertyChanged
{
    private readonly SearchWorkflowService _searchWorkflow;
    private readonly IFileLaunchService _fileLaunchService;
    private readonly IUserConfirmationService _confirmationService;
    private readonly IFileIntakeStore _store;
    private readonly Func<DateTimeOffset> _clock;

    private string _commandText = string.Empty;
    private string _resultsText = string.Empty;
    private string _statusMessage = "Ready";
    private bool _isBusy;
    private SearchResultItemViewModel? _selectedResult;
    private SearchWorkflowResult? _lastWorkflowResult;
    private long? _lastActionId;

    public SearchCommandViewModel(
        SearchWorkflowService searchWorkflow,
        IFileLaunchService fileLaunchService,
        IUserConfirmationService confirmationService,
        IFileIntakeStore store,
        Func<DateTimeOffset>? clock = null)
    {
        _searchWorkflow = searchWorkflow ?? throw new ArgumentNullException(nameof(searchWorkflow));
        _fileLaunchService = fileLaunchService ?? throw new ArgumentNullException(nameof(fileLaunchService));
        _confirmationService = confirmationService ?? throw new ArgumentNullException(nameof(confirmationService));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        RunCommand = new AsyncCommand(() => RunAsync(), () => !IsBusy);
        OpenSelectedFileCommand = new AsyncCommand(() => OpenSelectedFileAsync(), CanOpenSelectedFile);
        OpenContainingFolderCommand = new AsyncCommand(() => OpenContainingFolderAsync(), CanOpenContainingFolder);
        OpenAllCommand = new AsyncCommand(() => OpenAllAsync(), CanOpenAll);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AsyncCommand RunCommand { get; }

    public AsyncCommand OpenSelectedFileCommand { get; }

    public AsyncCommand OpenContainingFolderCommand { get; }

    public AsyncCommand OpenAllCommand { get; }

    public ObservableCollection<SearchResultItemViewModel> Results { get; } = [];

    public string CommandText
    {
        get => _commandText;
        set => SetField(ref _commandText, value);
    }

    public string ResultsText
    {
        get => _resultsText;
        private set => SetField(ref _resultsText, value);
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
                RaiseCommandStateChanged();
            }
        }
    }

    public SearchResultItemViewModel? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (SetField(ref _selectedResult, value))
            {
                RaiseCommandStateChanged();
            }
        }
    }

    public bool RequiresActionConfirmation => _lastWorkflowResult?.RequiresConfirmation == true;

    public bool HasAmbiguousOpenResults => _lastWorkflowResult?.Outcome == SearchExecutionOutcome.ShowBulkConfirmation;

    public long? LastActionId
    {
        get => _lastActionId;
        private set => SetField(ref _lastActionId, value);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CommandText))
        {
            StatusMessage = "Enter a search command first.";
            ResultsText = string.Empty;
            Results.Clear();
            SelectedResult = null;
            _lastWorkflowResult = null;
            RaiseSearchStateChanged();
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _searchWorkflow.ExecuteAsync(
                CommandText,
                _clock(),
                cancellationToken).ConfigureAwait(true);

            _lastWorkflowResult = result;
            Results.Clear();
            foreach (var item in result.Results)
            {
                Results.Add(new SearchResultItemViewModel(item));
            }

            SelectedResult = Results.FirstOrDefault();
            StatusMessage = result.Outcome switch
            {
                SearchExecutionOutcome.Unsupported => result.Intent.UnsupportedReason ?? "Command is unsupported.",
                SearchExecutionOutcome.ShowSingleConfirmation => "One match found. Confirm before opening.",
                SearchExecutionOutcome.ShowBulkConfirmation => $"{result.Results.Count} matches found. Confirmation required before opening.",
                _ => $"{result.Results.Count} matches found."
            };
            ResultsText = FormatResults(result);
            RaiseSearchStateChanged();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task OpenSelectedFileAsync(CancellationToken cancellationToken = default)
    {
        var selected = SelectedResult;
        if (selected is null)
        {
            StatusMessage = "Select a file result first.";
            return;
        }

        if (selected.Target != SearchResultTarget.File)
        {
            StatusMessage = "The selected result is a folder. Use Open Folder.";
            return;
        }

        await ConfirmAndLaunchAsync(
            [selected],
            SearchLaunchAction.OpenFile,
            cancellationToken).ConfigureAwait(true);
    }

    public async Task OpenContainingFolderAsync(CancellationToken cancellationToken = default)
    {
        var selected = SelectedResult;
        if (selected is null)
        {
            StatusMessage = "Select a result first.";
            return;
        }

        await ConfirmAndLaunchAsync(
            [selected],
            SearchLaunchAction.OpenContainingFolder,
            cancellationToken).ConfigureAwait(true);
    }

    public async Task OpenAllAsync(CancellationToken cancellationToken = default)
    {
        if (_lastWorkflowResult is null || Results.Count == 0)
        {
            StatusMessage = "Run a search command first.";
            return;
        }

        if (!_lastWorkflowResult.RequiresConfirmation || Results.Count <= 1)
        {
            StatusMessage = "Bulk open is only available for confirmed multi-result open commands.";
            return;
        }

        var action = _lastWorkflowResult.Intent.Action == SearchIntentAction.OpenContainingFolder
            ? SearchLaunchAction.OpenContainingFolder
            : SearchLaunchAction.OpenFile;

        await ConfirmAndLaunchAsync(Results.ToArray(), action, cancellationToken).ConfigureAwait(true);
    }

    private async Task ConfirmAndLaunchAsync(
        IReadOnlyList<SearchResultItemViewModel> items,
        SearchLaunchAction launchAction,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            StatusMessage = "No results selected.";
            return;
        }

        IsBusy = true;
        try
        {
            var paths = ResolveLaunchPaths(items, launchAction);
            if (paths.Count == 0)
            {
                StatusMessage = "No launchable paths were found for the selected result(s).";
                LastActionId = await LogSearchActionAsync(
                    items,
                    launchAction,
                    "Failed",
                    "Failed",
                    "No launchable paths were found.",
                    cancellationToken).ConfigureAwait(true);
                return;
            }

            var confirmed = await _confirmationService
                .ConfirmAsync(
                    items.Count == 1 ? "Confirm open" : "Confirm bulk open",
                    FormatLaunchConfirmation(paths, launchAction),
                    cancellationToken)
                .ConfigureAwait(true);

            if (!confirmed)
            {
                LastActionId = await LogSearchActionAsync(
                    items,
                    launchAction,
                    "Cancelled",
                    "Cancelled",
                    "User declined open confirmation.",
                    cancellationToken).ConfigureAwait(true);
                StatusMessage = "Open cancelled. No files or folders were opened.";
                return;
            }

            var failures = new List<string>();
            foreach (var path in paths)
            {
                var result = launchAction == SearchLaunchAction.OpenFile
                    ? await _fileLaunchService.OpenFileAsync(path, cancellationToken).ConfigureAwait(true)
                    : await _fileLaunchService.OpenFolderAsync(path, cancellationToken).ConfigureAwait(true);

                if (!result.Succeeded)
                {
                    failures.Add($"{path}: {result.FailureReason}");
                }
            }

            LastActionId = await LogSearchActionAsync(
                items,
                launchAction,
                failures.Count == 0 ? "Confirmed" : "Failed",
                failures.Count == 0 ? "Completed" : "Failed",
                failures.Count == 0 ? null : string.Join(Environment.NewLine, failures),
                cancellationToken).ConfigureAwait(true);

            StatusMessage = failures.Count == 0
                ? $"{paths.Count} path(s) opened after confirmation."
                : $"Open failed for {failures.Count} path(s).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FormatResults(SearchWorkflowResult result)
    {
        if (!result.Intent.IsSupported)
        {
            return result.Intent.UnsupportedReason ?? "Unsupported command.";
        }

        if (result.Results.Count == 0)
        {
            return "No matching SQLite metadata records were found.";
        }

        var builder = new StringBuilder();
        for (var index = 0; index < result.Results.Count; index++)
        {
            var item = result.Results[index];
            builder.Append(index + 1)
                .Append(". ")
                .Append(item.DisplayName)
                .Append(" [")
                .Append(item.Target)
                .AppendLine("]");
            builder.Append("   Path: ").AppendLine(item.Path);

            if (item.Project is not null || item.Topic is not null || item.Relevance is not null)
            {
                builder.Append("   Metadata: ")
                    .AppendJoin(
                        ", ",
                        new[] { item.Relevance, item.Project, item.Topic }
                            .Where(value => !string.IsNullOrWhiteSpace(value)))
                    .AppendLine();
            }

            builder.Append("   Match: ")
                .AppendJoin(", ", item.MatchedReasons)
                .AppendLine();
        }

        return builder.ToString();
    }

    private async Task<long> LogSearchActionAsync(
        IReadOnlyList<SearchResultItemViewModel> items,
        SearchLaunchAction launchAction,
        string actionTypeSuffix,
        string status,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        var now = _clock();
        var actionType = items.Count == 1
            ? $"{launchAction}{actionTypeSuffix}"
            : $"{launchAction}Multiple{actionTypeSuffix}";
        var firstFileResult = items.FirstOrDefault(item => item.Target == SearchResultTarget.File);

        return await _store.AddActionAsync(new FileActionRecord(
            Id: null,
            ActionType: actionType,
            TargetFileRecordId: items.Count == 1 && firstFileResult is not null
                ? firstFileResult.RecordId
                : null,
            OldPath: null,
            NewPath: null,
            Status: status,
            CreatedAt: now,
            CompletedAt: now,
            DetailsJson: JsonSerializer.Serialize(new
            {
                launchAction = launchAction.ToString(),
                failureReason,
                resultCount = items.Count,
                records = items.Select(item => new
                {
                    item.Target,
                    item.RecordId,
                    item.DisplayName,
                    item.Path,
                    item.ContainingFolder
                }).ToArray()
            })),
            cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<string> ResolveLaunchPaths(
        IReadOnlyList<SearchResultItemViewModel> items,
        SearchLaunchAction launchAction)
    {
        var paths = new List<string>();
        foreach (var item in items)
        {
            var path = launchAction switch
            {
                SearchLaunchAction.OpenFile when item.Target == SearchResultTarget.File => item.Path,
                SearchLaunchAction.OpenContainingFolder when item.Target == SearchResultTarget.Folder => item.Path,
                SearchLaunchAction.OpenContainingFolder => item.ContainingFolder ?? Path.GetDirectoryName(item.Path),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    private static string FormatLaunchConfirmation(
        IReadOnlyList<string> paths,
        SearchLaunchAction launchAction)
    {
        var label = launchAction == SearchLaunchAction.OpenFile
            ? "Open file path(s)?"
            : "Open folder path(s)?";

        var builder = new StringBuilder();
        builder.AppendLine(label);
        foreach (var path in paths)
        {
            builder.Append("- ").AppendLine(path);
        }

        return builder.ToString();
    }

    private bool CanOpenSelectedFile()
    {
        return !IsBusy && SelectedResult?.Target == SearchResultTarget.File;
    }

    private bool CanOpenContainingFolder()
    {
        return !IsBusy && SelectedResult is not null;
    }

    private bool CanOpenAll()
    {
        return !IsBusy
            && _lastWorkflowResult?.RequiresConfirmation == true
            && Results.Count > 1;
    }

    private void RaiseSearchStateChanged()
    {
        OnPropertyChanged(nameof(RequiresActionConfirmation));
        OnPropertyChanged(nameof(HasAmbiguousOpenResults));
        RaiseCommandStateChanged();
    }

    private void RaiseCommandStateChanged()
    {
        RunCommand.RaiseCanExecuteChanged();
        OpenSelectedFileCommand.RaiseCanExecuteChanged();
        OpenContainingFolderCommand.RaiseCanExecuteChanged();
        OpenAllCommand.RaiseCanExecuteChanged();
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private enum SearchLaunchAction
    {
        OpenFile,
        OpenContainingFolder
    }
}
