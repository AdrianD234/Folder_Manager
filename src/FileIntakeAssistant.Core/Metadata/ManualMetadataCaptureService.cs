using System.Text.Json;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;

namespace FileIntakeAssistant.Core.Metadata;

public sealed class ManualMetadataCaptureService
{
    private readonly IFileIntakeStore _store;

    public ManualMetadataCaptureService(IFileIntakeStore store)
    {
        _store = store;
    }

    public async Task<ManualMetadataCaptureResult> CaptureAsync(
        ManualFileSnapshot snapshot,
        ManualMetadataFields fields,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken = default,
        ManualMetadataCaptureContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(fields);
        context ??= ManualMetadataCaptureContext.Manual;

        var fullPath = NormalizePath(snapshot.Path);
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return Failed("A selected file path is required.");
        }

        if (!HasAnyMetadata(fields))
        {
            return Failed("At least one metadata field is required.");
        }

        var existingFileRecord = await _store
            .GetFileRecordByCurrentPathAsync(fullPath, cancellationToken)
            .ConfigureAwait(false);

        var fileRecordId = existingFileRecord?.Id ?? await _store.AddFileRecordAsync(new FileRecord(
            Id: null,
            Sha256: NullIfWhiteSpace(snapshot.Sha256),
            OriginalFilename: snapshot.FileName,
            CurrentFilename: snapshot.FileName,
            OriginalPath: fullPath,
            CurrentPath: fullPath,
            Extension: NormalizeExtension(snapshot.Extension),
            SizeBytes: snapshot.SizeBytes,
            MimeType: NullIfWhiteSpace(snapshot.MimeType),
            SourceIntakeFolderId: context.SourceIntakeFolderId,
            FirstSeenAt: capturedAt,
            LastSeenAt: capturedAt,
            StableAt: snapshot.LastWriteTimeUtc,
            Status: context.FileStatus,
            TriageCategory: context.TriageCategory,
            TriageConfidence: context.TriageConfidence,
            IsMeaningful: true,
            NotesJson: context.NotesJson ?? JsonSerializer.Serialize(new { source = context.Source })),
            cancellationToken).ConfigureAwait(false);

        var metadataEntryId = await _store.AddMetadataEntryAsync(new MetadataEntry(
            Id: null,
            FileRecordId: fileRecordId,
            FolderRecordId: null,
            UserNote: NullIfWhiteSpace(fields.UserNote),
            TranscriptText: NullIfWhiteSpace(fields.TranscriptText),
            Relevance: NormalizeRelevance(fields.Relevance),
            Project: NullIfWhiteSpace(fields.Project),
            Topic: NullIfWhiteSpace(fields.Topic),
            TagsJson: SerializeTags(fields.Tags),
            SourceUrl: NullIfWhiteSpace(fields.SourceUrl),
            ReferrerUrl: null,
            AgentSummary: null,
            ClassifierConfidence: null,
            CreatedAt: capturedAt,
            UpdatedAt: capturedAt),
            cancellationToken).ConfigureAwait(false);

        var actionId = await _store.AddActionAsync(new FileActionRecord(
            Id: null,
            ActionType: context.ActionType,
            TargetFileRecordId: fileRecordId,
            OldPath: fullPath,
            NewPath: fullPath,
            Status: "Completed",
            CreatedAt: capturedAt,
            CompletedAt: capturedAt,
            DetailsJson: JsonSerializer.Serialize(new
            {
                MetadataEntryId = metadataEntryId,
                context.Mode
            })),
            cancellationToken).ConfigureAwait(false);

        return new ManualMetadataCaptureResult(
            Succeeded: true,
            FileRecordId: fileRecordId,
            MetadataEntryId: metadataEntryId,
            ActionId: actionId,
            FailureReason: null);
    }

    private static ManualMetadataCaptureResult Failed(string reason)
    {
        return new ManualMetadataCaptureResult(false, null, null, null, reason);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Empty;
        }
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var value = extension.Trim();
        return value.StartsWith(".", StringComparison.Ordinal) ? value : $".{value}";
    }

    private static string? NormalizeRelevance(string? value)
    {
        var normalized = NullIfWhiteSpace(value)?.Trim().ToLowerInvariant();
        return normalized is "low" or "medium" or "high" ? normalized : normalized;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool HasAnyMetadata(ManualMetadataFields fields)
    {
        return NullIfWhiteSpace(fields.UserNote) is not null
            || NullIfWhiteSpace(fields.Relevance) is not null
            || NullIfWhiteSpace(fields.Project) is not null
            || NullIfWhiteSpace(fields.Topic) is not null
            || NullIfWhiteSpace(fields.Tags) is not null
            || NullIfWhiteSpace(fields.SourceUrl) is not null
            || NullIfWhiteSpace(fields.TranscriptText) is not null;
    }

    private static string? SerializeTags(string? tags)
    {
        var normalized = NullIfWhiteSpace(tags);
        if (normalized is null)
        {
            return null;
        }

        var values = normalized
            .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values.Length == 0 ? null : JsonSerializer.Serialize(values);
    }
}
