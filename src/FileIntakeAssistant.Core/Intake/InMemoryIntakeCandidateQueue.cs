namespace FileIntakeAssistant.Core.Intake;

public sealed class InMemoryIntakeCandidateQueue : IIntakeCandidateQueue
{
    private static readonly TimeSpan DefaultDeduplicationWindow = TimeSpan.FromSeconds(30);

    private readonly Queue<IntakeCandidate> _candidates = new();
    private readonly Dictionary<string, DateTimeOffset> _recentCandidateTimes = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _deduplicationWindow;
    private readonly object _sync = new();

    public InMemoryIntakeCandidateQueue()
        : this(DefaultDeduplicationWindow)
    {
    }

    public InMemoryIntakeCandidateQueue(TimeSpan deduplicationWindow)
    {
        if (deduplicationWindow < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(deduplicationWindow), "Deduplication window cannot be negative.");
        }

        _deduplicationWindow = deduplicationWindow;
    }

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
            var normalizedPath = NormalizePath(candidate.Path);
            var candidateTime = candidate.StableAt ?? candidate.ObservedAt;
            PruneRecentCandidates(candidateTime);
            if (IsDuplicateWithinWindow(normalizedPath, candidateTime))
            {
                return;
            }

            _candidates.Enqueue(candidate);
            _recentCandidateTimes[normalizedPath] = candidateTime;
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

    private bool IsDuplicateWithinWindow(string normalizedPath, DateTimeOffset candidateTime)
    {
        if (!_recentCandidateTimes.TryGetValue(normalizedPath, out var previousTime))
        {
            return false;
        }

        var age = candidateTime >= previousTime
            ? candidateTime - previousTime
            : previousTime - candidateTime;
        return age <= _deduplicationWindow;
    }

    private void PruneRecentCandidates(DateTimeOffset now)
    {
        if (_deduplicationWindow == TimeSpan.Zero)
        {
            _recentCandidateTimes.Clear();
            return;
        }

        foreach (var stalePath in _recentCandidateTimes
            .Where(candidate => now - candidate.Value > _deduplicationWindow)
            .Select(candidate => candidate.Key)
            .ToArray())
        {
            _recentCandidateTimes.Remove(stalePath);
        }
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().Replace('/', '\\').TrimEnd('\\');
    }
}
