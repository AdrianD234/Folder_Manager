using System.ComponentModel;
using System.Runtime.CompilerServices;
using FileIntakeAssistant.Core.Intake;
using FileIntakeAssistant.Core.Metadata;

namespace FileIntakeAssistant.App.ViewModels;

public sealed class IntakeCandidatePopupViewModel : INotifyPropertyChanged
{
    private readonly IntakeCandidateWorkflowService _workflowService;
    private readonly Func<DateTimeOffset> _clock;

    private string _userNote = string.Empty;
    private string _transcriptText = string.Empty;
    private string _relevance = "medium";
    private string _project = string.Empty;
    private string _topic = string.Empty;
    private string _tags = string.Empty;
    private string _sourceUrl = string.Empty;
    private string _statusMessage = "Ready";
    private bool _isBusy;
    private bool _isComplete;
    private long? _metadataEntryId;
    private long? _actionId;
    private long? _manualTranscriptionJobId;

    public IntakeCandidatePopupViewModel(
        IntakeCandidate candidate,
        IntakeCandidateWorkflowService workflowService,
        Func<DateTimeOffset>? clock = null)
    {
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);

        SaveCommand = new AsyncCommand(() => SaveAsync(), CanExecuteWorkflowAction);
        SkipCommand = new AsyncCommand(() => SkipAsync("User skipped candidate popup."), CanExecuteWorkflowAction);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? RequestClose;

    public IntakeCandidate Candidate { get; }

    public AsyncCommand SaveCommand { get; }

    public AsyncCommand SkipCommand { get; }

    public string FileName => Candidate.FileName;

    public string Path => Candidate.Path;

    public string Extension => Candidate.Extension;

    public long SizeBytes => Candidate.SizeBytes;

    public string TriageReason => Candidate.TriageReason;

    public string TriageCategory => Candidate.TriageCategory.ToString();

    public double TriageConfidence => Candidate.TriageConfidence;

    public string SourceIntakeFolderPath => Candidate.SourceIntakeFolderPath;

    public string StabilityEvidence => Candidate.StableAt is null
        ? $"Accepted as stable by the intake workflow. Hash plan: {Candidate.HashPlan}."
        : $"Stable at {Candidate.StableAt:yyyy-MM-dd HH:mm:ss zzz}. Hash plan: {Candidate.HashPlan}.";

    public string BatchEvidence => "No suppressing batch was detected for this candidate.";

    public string ProviderStatusText => "Manual transcript mode. OpenAI and local transcription are disabled until configured.";

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
                RaiseCommandStateChanged();
            }
        }
    }

    public bool IsComplete
    {
        get => _isComplete;
        private set
        {
            if (SetField(ref _isComplete, value))
            {
                RaiseCommandStateChanged();
            }
        }
    }

    public long? MetadataEntryId
    {
        get => _metadataEntryId;
        private set => SetField(ref _metadataEntryId, value);
    }

    public long? ActionId
    {
        get => _actionId;
        private set => SetField(ref _actionId, value);
    }

    public long? ManualTranscriptionJobId
    {
        get => _manualTranscriptionJobId;
        private set => SetField(ref _manualTranscriptionJobId, value);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (IsComplete)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _workflowService.SaveMetadataAsync(
                Candidate,
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

            if (result.Status != IntakeCandidateWorkflowStatus.Succeeded)
            {
                StatusMessage = result.ErrorMessage ?? "Candidate metadata was not saved.";
                return;
            }

            MetadataEntryId = result.MetadataEntryId;
            ActionId = result.ActionId;
            ManualTranscriptionJobId = result.ManualTranscriptionJobId;
            StatusMessage = $"Saved metadata record {result.MetadataEntryId}.";
            IsComplete = true;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SkipAsync(
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (IsComplete)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _workflowService.SkipAsync(
                Candidate,
                reason,
                _clock(),
                cancellationToken).ConfigureAwait(true);

            if (result.Status != IntakeCandidateWorkflowStatus.Succeeded)
            {
                StatusMessage = result.ErrorMessage ?? "Candidate was not skipped.";
                return;
            }

            ActionId = result.ActionId;
            StatusMessage = $"Skipped candidate with audit action {result.ActionId}.";
            IsComplete = true;
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanExecuteWorkflowAction()
    {
        return !IsBusy && !IsComplete;
    }

    private void RaiseCommandStateChanged()
    {
        SaveCommand.RaiseCanExecuteChanged();
        SkipCommand.RaiseCanExecuteChanged();
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
