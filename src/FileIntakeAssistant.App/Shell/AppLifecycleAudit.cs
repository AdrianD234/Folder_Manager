using FileIntakeAssistant.Infrastructure.Logging;

namespace FileIntakeAssistant.App.Shell;

public sealed class AppLifecycleAudit
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyFields =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    private readonly ILocalAuditLog _auditLog;

    public AppLifecycleAudit(ILocalAuditLog auditLog)
    {
        _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
    }

    public async Task WriteAsync(
        string eventType,
        string status,
        IReadOnlyDictionary<string, object?>? fields = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _auditLog
                .WriteAsync(eventType, status, fields ?? EmptyFields, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // Audit logging must never break tray, hotkey, watcher, or shutdown paths.
        }
    }

    public void WriteFireAndForget(
        string eventType,
        string status,
        IReadOnlyDictionary<string, object?>? fields = null)
    {
        _ = WriteAsync(eventType, status, fields);
    }
}
