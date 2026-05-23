using System.ComponentModel;
using System.Runtime.CompilerServices;
using FileIntakeAssistant.Core.Metadata;

namespace FileIntakeAssistant.App.ViewModels;

public sealed class ManualMetadataCaptureViewModel : INotifyPropertyChanged
{
    private readonly IManualFileSnapshotReader _snapshotReader;
    private readonly ManualMetadataCaptureService _captureService;
    private readonly Func<DateTimeOffset> _clock;

    private string _filePath = string.Empty;
    private string _userNote = string.Empty;
    private string _transcriptText = string.Empty;
    private string _relevance = "medium";
    private string _project = string.Empty;
    private string _topic = string.Empty;
    private string _tags = string.Empty;
    private string _sourceUrl = string.Empty;
    private string _statusMessage = "Ready";
    private bool _isBusy;

    public ManualMetadataCaptureViewModel(
        IManualFileSnapshotReader snapshotReader,
        ManualMetadataCaptureService captureService,
        Func<DateTimeOffset>? clock = null)
    {
        _snapshotReader = snapshotReader;
        _captureService = captureService;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        SaveCommand = new AsyncCommand(() => SaveAsync(), () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AsyncCommand SaveCommand { get; }

    public string FilePath
    {
        get => _filePath;
        set => SetField(ref _filePath, value);
    }

    public string UserNote
    {
        get => _userNote;
        set => SetField(ref _userNote, value);
    }

    public string TranscriptText
    {
        get => _transcriptText;
        set => SetField(ref _transcriptText, value);
    }

    public string Relevance
    {
        get => _relevance;
        set => SetField(ref _relevance, value);
    }

    public string Project
    {
        get => _project;
        set => SetField(ref _project, value);
    }

    public string Topic
    {
        get => _topic;
        set => SetField(ref _topic, value);
    }

    public string Tags
    {
        get => _tags;
        set => SetField(ref _tags, value);
    }

    public string SourceUrl
    {
        get => _sourceUrl;
        set => SetField(ref _sourceUrl, value);
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
                SaveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            StatusMessage = "Select a file first.";
            return;
        }

        IsBusy = true;
        try
        {
            var snapshot = await _snapshotReader.ReadAsync(FilePath, cancellationToken).ConfigureAwait(true);
            if (snapshot is null)
            {
                StatusMessage = "Selected file was not found.";
                return;
            }

            var result = await _captureService.CaptureAsync(
                snapshot,
                new ManualMetadataFields(
                    UserNote,
                    Relevance,
                    Project,
                    Topic,
                    Tags,
                    SourceUrl,
                    TranscriptText),
                _clock(),
                cancellationToken).ConfigureAwait(true);

            StatusMessage = result.Succeeded
                ? $"Saved metadata record {result.MetadataEntryId}."
                : result.FailureReason ?? "Metadata was not saved.";
        }
        finally
        {
            IsBusy = false;
        }
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
