namespace FileIntakeAssistant.Core.Triage;

public interface IOwnOperationSuppressionRegistry
{
    void RegisterMoveOrRename(
        string? oldPath,
        string? newPath,
        DateTimeOffset registeredAt,
        TimeSpan? suppressionWindow = null);

    IReadOnlyCollection<OwnOperationSuppression> GetActiveSuppressions(DateTimeOffset observedAt);
}

public sealed class OwnOperationSuppressionRegistry : IOwnOperationSuppressionRegistry
{
    private readonly object _sync = new();
    private readonly List<OwnOperationSuppression> _suppressions = [];

    public void RegisterMoveOrRename(
        string? oldPath,
        string? newPath,
        DateTimeOffset registeredAt,
        TimeSpan? suppressionWindow = null)
    {
        if (string.IsNullOrWhiteSpace(oldPath) && string.IsNullOrWhiteSpace(newPath))
        {
            throw new ArgumentException("At least one path is required for own-operation suppression.");
        }

        var window = suppressionWindow ?? OwnOperationSuppression.DefaultSuppressionWindow;
        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(suppressionWindow), "Suppression window must be positive.");
        }

        var suppression = new OwnOperationSuppression(
            OldPath: NormalizePath(oldPath),
            NewPath: NormalizePath(newPath),
            RegisteredAt: registeredAt,
            SuppressionWindow: window);

        lock (_sync)
        {
            PruneExpired(registeredAt);
            _suppressions.Add(suppression);
        }
    }

    public IReadOnlyCollection<OwnOperationSuppression> GetActiveSuppressions(DateTimeOffset observedAt)
    {
        lock (_sync)
        {
            PruneExpired(observedAt);
            return _suppressions
                .Where(suppression => observedAt >= suppression.RegisteredAt
                    && observedAt - suppression.RegisteredAt <= suppression.SuppressionWindow)
                .ToArray();
        }
    }

    private void PruneExpired(DateTimeOffset now)
    {
        _suppressions.RemoveAll(suppression => now - suppression.RegisteredAt > suppression.SuppressionWindow);
    }

    private static string? NormalizePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? null
            : path.Trim().Replace('/', '\\').TrimEnd('\\');
    }
}
