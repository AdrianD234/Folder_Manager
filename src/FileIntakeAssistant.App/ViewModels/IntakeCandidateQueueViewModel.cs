using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FileIntakeAssistant.Core.Intake;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;

namespace FileIntakeAssistant.App.ViewModels;

public sealed class IntakeCandidateQueueViewModel : INotifyPropertyChanged
{
    private readonly IIntakeCandidateQueue _candidateQueue;
    private readonly IFileIntakeStore _store;

    private string _statusMessage = "Ready";
    private string _auditText = string.Empty;
    private bool _isBusy;

    public IntakeCandidateQueueViewModel(
        IIntakeCandidateQueue candidateQueue,
        IFileIntakeStore store)
    {
        _candidateQueue = candidateQueue ?? throw new ArgumentNullException(nameof(candidateQueue));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        RefreshCommand = new AsyncCommand(() => RefreshAsync(), () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<IntakeCandidateItemViewModel> Candidates { get; } = [];

    public AsyncCommand RefreshCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public string AuditText
    {
        get => _auditText;
        private set => SetField(ref _auditText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            Candidates.Clear();
            foreach (var candidate in _candidateQueue.Snapshot())
            {
                Candidates.Add(new IntakeCandidateItemViewModel(candidate));
            }

            var events = await _store.ListFileEventsAsync(limit: 25, cancellationToken).ConfigureAwait(true);
            AuditText = FormatAudit(events);
            StatusMessage = $"{Candidates.Count} candidates queued. {events.Count} recent audit events loaded.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FormatAudit(IReadOnlyList<FileEventRecord> events)
    {
        if (events.Count == 0)
        {
            return "No file event audit rows have been recorded yet.";
        }

        return string.Join(
            Environment.NewLine,
            events.Select(fileEvent =>
                $"{fileEvent.ObservedAt:yyyy-MM-dd HH:mm:ss} | {fileEvent.EventType} | {fileEvent.TriageCategory} | {(fileEvent.Ignored ? "ignored" : "active")} | {fileEvent.RawPath}"));
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
