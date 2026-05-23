namespace FileIntakeAssistant.Core.Intake;

public interface IIntakeCandidateQueue
{
    int Count { get; }

    void Enqueue(IntakeCandidate candidate);

    bool TryDequeue(out IntakeCandidate? candidate);

    IReadOnlyList<IntakeCandidate> Snapshot();
}
