namespace FileIntakeAssistant.Core.FileOperations;

public enum SafeFileOperationKind
{
    Move,
    Rename
}

public sealed record SafeFileOperationPlanRequest(
    string SourcePath,
    string DestinationDirectory,
    string RequestedFileName,
    SafeFileOperationKind OperationKind,
    bool DestinationDirectoryExists,
    IReadOnlyCollection<string> ExistingDestinationPaths,
    bool AllowExtensionChange = false);

public sealed record SafeFileOperationPlan(
    Guid PlanId,
    SafeFileOperationKind OperationKind,
    string SourcePath,
    string DestinationDirectory,
    string RequestedFileName,
    string SanitizedFileName,
    string DestinationPath,
    bool RequiresConfirmation,
    bool WouldCreateDestinationDirectory,
    bool ExtensionPreserved,
    bool IsValid,
    IReadOnlyList<string> ValidationErrors);

public sealed record SafeFileOperationConfirmation(
    Guid PlanId,
    bool Confirmed,
    DateTimeOffset ConfirmedAt);

public sealed record FileIdentitySnapshot(
    string Path,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc,
    string? Sha256);

public sealed record SafeFileOperationExecutionResult(
    bool Succeeded,
    long? ActionId,
    long? UndoActionId,
    string? FailureReason,
    string SourcePath,
    string DestinationPath);

public sealed record UndoExecutionResult(
    bool Succeeded,
    long UndoActionId,
    string? FailureReason,
    string OriginalPath,
    string ResultingPath);
