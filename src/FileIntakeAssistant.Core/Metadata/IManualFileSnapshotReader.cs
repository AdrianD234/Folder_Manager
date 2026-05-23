namespace FileIntakeAssistant.Core.Metadata;

public interface IManualFileSnapshotReader
{
    Task<ManualFileSnapshot?> ReadAsync(string path, CancellationToken cancellationToken = default);
}
