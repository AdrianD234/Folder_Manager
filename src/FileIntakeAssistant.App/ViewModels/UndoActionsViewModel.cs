using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;
using FileIntakeAssistant.Infrastructure.FileSystem;

namespace FileIntakeAssistant.App.ViewModels;

public sealed class UndoActionsViewModel : INotifyPropertyChanged
{
    private readonly IFileIntakeStore _store;
    private readonly SafeFileOperationExecutor _executor;
    private readonly IUserConfirmationService _confirmationService;
    private readonly Func<DateTimeOffset> _clock;

    private UndoActionItemViewModel? _selectedUndoAction;
    private string _statusMessage = "Ready";
    private bool _isBusy;
    private int _limit = 100;
    private long? _lastAuditActionId;

    public UndoActionsViewModel(
        IFileIntakeStore store,
        SafeFileOperationExecutor executor,
        IUserConfirmationService confirmationService,
        Func<DateTimeOffset>? clock = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _confirmationService = confirmationService ?? throw new ArgumentNullException(nameof(confirmationService));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);

        RefreshCommand = new AsyncCommand(() => RefreshAsync(), () => !IsBusy);
        UndoCommand = new AsyncCommand(() => UndoSelectedAsync(), CanUndoSelected);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<UndoActionItemViewModel> UndoActions { get; } = [];

    public AsyncCommand RefreshCommand { get; }

    public AsyncCommand UndoCommand { get; }

    public UndoActionItemViewModel? SelectedUndoAction
    {
        get => _selectedUndoAction;
        set
        {
            if (SetField(ref _selectedUndoAction, value))
            {
                UndoCommand.RaiseCanExecuteChanged();
            }
        }
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
                RefreshCommand.RaiseCanExecuteChanged();
                UndoCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int Limit
    {
        get => _limit;
        set => SetField(ref _limit, Math.Max(1, value));
    }

    public long? LastAuditActionId
    {
        get => _lastAuditActionId;
        private set => SetField(ref _lastAuditActionId, value);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            var records = await _store.ListUndoActionsAsync(
                status: "Pending",
                limit: Limit,
                cancellationToken).ConfigureAwait(true);

            UndoActions.Clear();
            foreach (var record in records)
            {
                UndoActions.Add(new UndoActionItemViewModel(record));
            }

            SelectedUndoAction = UndoActions.FirstOrDefault();
            StatusMessage = $"{UndoActions.Count} pending undo action(s).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task UndoSelectedAsync(CancellationToken cancellationToken = default)
    {
        var selected = SelectedUndoAction;
        if (selected is null || selected.Id <= 0)
        {
            StatusMessage = "Select a pending undo action first.";
            return;
        }

        IsBusy = true;
        try
        {
            var confirmed = await _confirmationService
                .ConfirmAsync(
                    "Confirm undo",
                    FormatUndoConfirmation(selected),
                    cancellationToken)
                .ConfigureAwait(true);

            if (!confirmed)
            {
                LastAuditActionId = await LogUndoAuditActionAsync(
                    selected,
                    "UndoCancelled",
                    "Cancelled",
                    "User declined undo confirmation.",
                    cancellationToken).ConfigureAwait(true);
                StatusMessage = "Undo cancelled. No file changes were made.";
                return;
            }

            var now = _clock();
            var result = await _executor
                .UndoAsync(selected.Id, now, cancellationToken)
                .ConfigureAwait(true);

            LastAuditActionId = await LogUndoAuditActionAsync(
                selected,
                result.Succeeded ? "UndoPerformed" : "UndoFailed",
                result.Succeeded ? "Completed" : "Failed",
                result.FailureReason,
                cancellationToken).ConfigureAwait(true);

            StatusMessage = result.Succeeded
                ? "Undo completed."
                : result.FailureReason ?? "Undo failed.";

            await RefreshAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<long> LogUndoAuditActionAsync(
        UndoActionItemViewModel selected,
        string actionType,
        string status,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        var now = _clock();
        return await _store.AddActionAsync(new FileActionRecord(
            Id: null,
            ActionType: actionType,
            TargetFileRecordId: selected.TargetFileRecordId,
            OldPath: selected.ResultingPath,
            NewPath: selected.OriginalPath,
            Status: status,
            CreatedAt: now,
            CompletedAt: now,
            DetailsJson: JsonSerializer.Serialize(new
            {
                undoActionId = selected.Id,
                selected.UndoType,
                failureReason
            })),
            cancellationToken).ConfigureAwait(false);
    }

    private bool CanUndoSelected()
    {
        return !IsBusy && SelectedUndoAction?.Status == "Pending";
    }

    private static string FormatUndoConfirmation(UndoActionItemViewModel selected)
    {
        return string.Join(
            Environment.NewLine,
            "Restore the file to its original path?",
            $"Current: {selected.ResultingPath}",
            $"Original: {selected.OriginalPath}",
            "Undo will fail safely if the original path is occupied or the file identity has changed.");
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
