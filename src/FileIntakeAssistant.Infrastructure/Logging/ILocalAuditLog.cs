namespace FileIntakeAssistant.Infrastructure.Logging;

public interface ILocalAuditLog
{
    Task WriteAsync(
        string eventType,
        string status,
        IReadOnlyDictionary<string, object?> fields,
        CancellationToken cancellationToken = default);
}
