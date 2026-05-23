using FileIntakeAssistant.Core.Intake;

namespace FileIntakeAssistant.App.ViewModels;

public sealed class IntakeCandidateItemViewModel
{
    public IntakeCandidateItemViewModel(IntakeCandidate candidate)
    {
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
    }

    public IntakeCandidate Candidate { get; }

    public string FileName => Candidate.FileName;

    public string Path => Candidate.Path;

    public string Extension => Candidate.Extension;

    public long SizeBytes => Candidate.SizeBytes;

    public string TriageReason => Candidate.TriageReason;

    public string TriageCategory => Candidate.TriageCategory.ToString();

    public DateTimeOffset ObservedAt => Candidate.ObservedAt;

    public string SourceIntakeFolderPath => Candidate.SourceIntakeFolderPath;
}
