using System.Text.Json;
using FileIntakeAssistant.Core.FileOperations;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;

namespace FileIntakeAssistant.Infrastructure.FileSystem;

public sealed class SafeFileOperationExecutor
{
    private readonly IFileIntakeStore _store;
    private readonly FileOperationIdentityReader _identityReader;

    public SafeFileOperationExecutor(IFileIntakeStore store)
        : this(store, new FileOperationIdentityReader())
    {
    }

    internal SafeFileOperationExecutor(
        IFileIntakeStore store,
        FileOperationIdentityReader identityReader)
    {
        _store = store;
        _identityReader = identityReader;
    }

    public async Task<SafeFileOperationExecutionResult> ExecuteAsync(
        SafeFileOperationPlan plan,
        SafeFileOperationConfirmation confirmation,
        long targetFileRecordId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(confirmation);

        var confirmationFailure = ValidateConfirmation(plan, confirmation);
        if (confirmationFailure is not null)
        {
            return FailedExecution(plan, confirmationFailure);
        }

        if (!File.Exists(plan.SourcePath))
        {
            return FailedExecution(plan, "The source file does not exist.");
        }

        if (File.Exists(plan.DestinationPath) || Directory.Exists(plan.DestinationPath))
        {
            return FailedExecution(plan, "The destination path already exists.");
        }

        var fileRecord = await _store.GetFileRecordAsync(targetFileRecordId, cancellationToken).ConfigureAwait(false);
        if (fileRecord is null)
        {
            return FailedExecution(plan, "The target file record does not exist.");
        }

        var sourceIdentity = await _identityReader
            .ReadAsync(plan.SourcePath, plan.DestinationPath, cancellationToken)
            .ConfigureAwait(false);

        var actionId = await _store.AddActionAsync(new FileActionRecord(
            Id: null,
            ActionType: plan.OperationKind.ToString(),
            TargetFileRecordId: targetFileRecordId,
            OldPath: plan.SourcePath,
            NewPath: plan.DestinationPath,
            Status: "Pending",
            CreatedAt: now,
            CompletedAt: null,
            DetailsJson: JsonSerializer.Serialize(new
            {
                plan.PlanId,
                confirmation.ConfirmedAt,
                plan.WouldCreateDestinationDirectory
            })),
            cancellationToken).ConfigureAwait(false);

        var undoActionId = await _store.AddUndoActionAsync(new UndoActionRecord(
            Id: null,
            ActionId: actionId,
            TargetFileRecordId: targetFileRecordId,
            UndoType: plan.OperationKind == SafeFileOperationKind.Rename ? "RenameBack" : "MoveBack",
            OriginalPath: plan.SourcePath,
            ResultingPath: plan.DestinationPath,
            FileIdentityJson: JsonSerializer.Serialize(sourceIdentity),
            Status: "Pending",
            CreatedAt: now,
            PerformedAt: null),
            cancellationToken).ConfigureAwait(false);

        try
        {
            if (File.Exists(plan.DestinationPath) || Directory.Exists(plan.DestinationPath))
            {
                throw new IOException("The destination path already exists.");
            }

            var destinationDirectory = Path.GetDirectoryName(plan.DestinationPath);
            if (string.IsNullOrWhiteSpace(destinationDirectory))
            {
                throw new InvalidOperationException("The destination directory is invalid.");
            }

            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Move(plan.SourcePath, plan.DestinationPath);

            var completedAction = new FileActionRecord(
                Id: actionId,
                ActionType: plan.OperationKind.ToString(),
                TargetFileRecordId: targetFileRecordId,
                OldPath: plan.SourcePath,
                NewPath: plan.DestinationPath,
                Status: "Completed",
                CreatedAt: now,
                CompletedAt: now,
                DetailsJson: JsonSerializer.Serialize(new
                {
                    plan.PlanId,
                    confirmation.ConfirmedAt,
                    plan.WouldCreateDestinationDirectory
                }));

            var updatedRecord = fileRecord with
            {
                Sha256 = sourceIdentity.Sha256 ?? fileRecord.Sha256,
                CurrentFilename = Path.GetFileName(plan.DestinationPath),
                CurrentPath = plan.DestinationPath,
                Extension = Path.GetExtension(plan.DestinationPath),
                SizeBytes = sourceIdentity.SizeBytes,
                LastSeenAt = now,
                Status = "Filed"
            };

            await _store.UpdateFileRecordAsync(updatedRecord, cancellationToken).ConfigureAwait(false);
            await _store.UpdateActionAsync(completedAction, cancellationToken).ConfigureAwait(false);

            return new SafeFileOperationExecutionResult(
                Succeeded: true,
                ActionId: actionId,
                UndoActionId: undoActionId,
                FailureReason: null,
                SourcePath: plan.SourcePath,
                DestinationPath: plan.DestinationPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await MarkActionAndUndoFailedAsync(
                actionId,
                undoActionId,
                targetFileRecordId,
                plan,
                now,
                JsonSerializer.Serialize(sourceIdentity),
                ex.Message,
                cancellationToken).ConfigureAwait(false);

            return new SafeFileOperationExecutionResult(
                Succeeded: false,
                ActionId: actionId,
                UndoActionId: undoActionId,
                FailureReason: ex.Message,
                SourcePath: plan.SourcePath,
                DestinationPath: plan.DestinationPath);
        }
    }

    public async Task<UndoExecutionResult> UndoAsync(
        long undoActionId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var undoAction = await _store.GetUndoActionAsync(undoActionId, cancellationToken).ConfigureAwait(false);
        if (undoAction is null)
        {
            return new UndoExecutionResult(
                Succeeded: false,
                UndoActionId: undoActionId,
                FailureReason: "The undo action does not exist.",
                OriginalPath: string.Empty,
                ResultingPath: string.Empty);
        }

        var failure = await ValidateUndoAsync(undoAction, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            await _store.UpdateUndoActionAsync(undoAction with
            {
                Status = "Failed"
            }, cancellationToken).ConfigureAwait(false);

            return new UndoExecutionResult(
                Succeeded: false,
                UndoActionId: undoActionId,
                FailureReason: failure,
                OriginalPath: undoAction.OriginalPath,
                ResultingPath: undoAction.ResultingPath);
        }

        try
        {
            File.Move(undoAction.ResultingPath, undoAction.OriginalPath);

            var fileRecord = await _store.GetFileRecordAsync(
                undoAction.TargetFileRecordId,
                cancellationToken).ConfigureAwait(false);

            if (fileRecord is not null)
            {
                await _store.UpdateFileRecordAsync(fileRecord with
                {
                    CurrentFilename = Path.GetFileName(undoAction.OriginalPath),
                    CurrentPath = undoAction.OriginalPath,
                    Extension = Path.GetExtension(undoAction.OriginalPath),
                    LastSeenAt = now,
                    Status = "Candidate"
                }, cancellationToken).ConfigureAwait(false);
            }

            await _store.UpdateUndoActionAsync(undoAction with
            {
                Status = "Performed",
                PerformedAt = now
            }, cancellationToken).ConfigureAwait(false);

            return new UndoExecutionResult(
                Succeeded: true,
                UndoActionId: undoActionId,
                FailureReason: null,
                OriginalPath: undoAction.OriginalPath,
                ResultingPath: undoAction.ResultingPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await _store.UpdateUndoActionAsync(undoAction with
            {
                Status = "Failed"
            }, cancellationToken).ConfigureAwait(false);

            return new UndoExecutionResult(
                Succeeded: false,
                UndoActionId: undoActionId,
                FailureReason: ex.Message,
                OriginalPath: undoAction.OriginalPath,
                ResultingPath: undoAction.ResultingPath);
        }
    }

    private async Task<string?> ValidateUndoAsync(
        UndoActionRecord undoAction,
        CancellationToken cancellationToken)
    {
        if (undoAction.Status != "Pending")
        {
            return "The undo action is not pending.";
        }

        if (!File.Exists(undoAction.ResultingPath))
        {
            return "The resulting file no longer exists.";
        }

        if (File.Exists(undoAction.OriginalPath) || Directory.Exists(undoAction.OriginalPath))
        {
            return "The original path is occupied.";
        }

        var originalDirectory = Path.GetDirectoryName(undoAction.OriginalPath);
        if (string.IsNullOrWhiteSpace(originalDirectory) || !Directory.Exists(originalDirectory))
        {
            return "The original directory does not exist.";
        }

        var expectedIdentity = JsonSerializer.Deserialize<FileIdentitySnapshot>(undoAction.FileIdentityJson);
        if (expectedIdentity is null)
        {
            return "The stored file identity is invalid.";
        }

        var actualIdentity = await _identityReader
            .ReadAsync(undoAction.ResultingPath, undoAction.ResultingPath, cancellationToken)
            .ConfigureAwait(false);

        return FileOperationIdentityReader.Matches(expectedIdentity, actualIdentity)
            ? null
            : "The current file identity does not match the undo record.";
    }

    private async Task MarkActionAndUndoFailedAsync(
        long actionId,
        long undoActionId,
        long targetFileRecordId,
        SafeFileOperationPlan plan,
        DateTimeOffset now,
        string fileIdentityJson,
        string message,
        CancellationToken cancellationToken)
    {
        await _store.UpdateActionAsync(new FileActionRecord(
            Id: actionId,
            ActionType: plan.OperationKind.ToString(),
            TargetFileRecordId: targetFileRecordId,
            OldPath: plan.SourcePath,
            NewPath: plan.DestinationPath,
            Status: "Failed",
            CreatedAt: now,
            CompletedAt: now,
            DetailsJson: JsonSerializer.Serialize(new
            {
                plan.PlanId,
                Error = message
            })),
            cancellationToken).ConfigureAwait(false);

        await _store.UpdateUndoActionAsync(new UndoActionRecord(
            Id: undoActionId,
            ActionId: actionId,
            TargetFileRecordId: targetFileRecordId,
            UndoType: plan.OperationKind == SafeFileOperationKind.Rename ? "RenameBack" : "MoveBack",
            OriginalPath: plan.SourcePath,
            ResultingPath: plan.DestinationPath,
            FileIdentityJson: fileIdentityJson,
            Status: "Failed",
            CreatedAt: now,
            PerformedAt: null),
            cancellationToken).ConfigureAwait(false);
    }

    private static string? ValidateConfirmation(
        SafeFileOperationPlan plan,
        SafeFileOperationConfirmation confirmation)
    {
        if (!plan.IsValid)
        {
            return string.Join(" ", plan.ValidationErrors);
        }

        if (!plan.RequiresConfirmation)
        {
            return null;
        }

        if (!confirmation.Confirmed || confirmation.PlanId != plan.PlanId)
        {
            return "The file operation was not explicitly confirmed.";
        }

        return null;
    }

    private static SafeFileOperationExecutionResult FailedExecution(
        SafeFileOperationPlan plan,
        string failureReason)
    {
        return new SafeFileOperationExecutionResult(
            Succeeded: false,
            ActionId: null,
            UndoActionId: null,
            FailureReason: failureReason,
            SourcePath: plan.SourcePath,
            DestinationPath: plan.DestinationPath);
    }
}
