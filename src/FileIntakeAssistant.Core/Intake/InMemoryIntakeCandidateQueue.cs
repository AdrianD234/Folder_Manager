namespace FileIntakeAssistant.Core.Intake;

public sealed class InMemoryIntakeCandidateQueue : IIntakeCandidateQueue
{
    private readonly Queue<IntakeCandidate> _candidates = new();
    private readonly object _sync = new();

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _candidates.Count;
            }
        }
    }

    public void Enqueue(IntakeCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        lock (_sync)
        {
            _candidates.Enqueue(candidate);
        }
    }

    public bool TryDequeue(out IntakeCandidate? candidate)
    {
        lock (_sync)
        {
            if (_candidates.Count == 0)
            {
                candidate = null;
                return false;
            }

            candidate = _candidates.Dequeue();
            return true;
        }
    }

    public IReadOnlyList<IntakeCandidate> Snapshot()
    {
        lock (_sync)
        {
            return _candidates.ToArray();
        }
    }
}
