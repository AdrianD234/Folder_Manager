using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FileIntakeAssistant.Core.FileOperations;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;
using FileIntakeAssistant.Infrastructure.FileSystem;

namespace FileIntakeAssistant.App.ViewModels;

public sealed class SafeFileOperationViewModel : INotifyPropertyChanged
{
    private readonly IFileIntakeStore _store;
    private readonly SafeFileOperationExecutor _executor;
    private readonly SafeFileOperationPlanner _planner;
    private readonly IUserConfirmationService _confirmationService;
    private readonly Func<DateTimeOffset> _clock;

    private string _fileRecordId = string.Empty;
    private string _sourcePath = string.Empty;
    private string _destinationDirectory = string.Empty;
    private string _requestedFileName = string.Empty;
    private SafeFileOperationKind _operationKind = SafeFileOperationKind.Move;
    private string _previewText = "Enter a source file and destination to preview the operation.";
    private string _statusMessage = "Ready";
    private bool _isBusy;
    private SafeFileOperationPlan? _currentPlan;
    private long? _lastActionId;
    private long? _lastUndoActionId;

    public SafeFileOperationViewModel(
        IFileIntakeStore store,
        SafeFileOperationExecutor executor,
        IUserConfirmationService confirmationService,
        Func<DateTimeOffset>? clock = null)
        : this(store, executor, new SafeFileOperationPlanner(), confirmationService, clock)
    {
    }

    public SafeFileOperationViewModel(
        IFileIntakeStore store,
        SafeFileOperationExecutor executor,
        SafeFileOperationPlanner planner,
        IUserConfirmationService confirmationService,
        Func<DateTimeOffset>? clock = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _confirmationService = confirmationService ?? throw new ArgumentNullException(nameof(confirmationService));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);

        RefreshPreviewCommand = new AsyncCommand(() => RefreshPreviewAsync(), () => !IsBusy);
        ConfirmOperationCommand = new AsyncCommand(() => ConfirmOperationAsync(), CanConfirmOperation);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AsyncCommand RefreshPreviewCommand { get; }

    public AsyncCommand ConfirmOperationCommand { get; }

    public string FileRecordId
    {
        get => _fileRecordId;
        set
        {
            if (SetField(ref _fileRecordId, value))
            {
                RaiseCommandStateChanged();
            }
        }
    }

    public string SourcePath
    {
        get => _sourcePath;
        set
        {
            if (SetField(ref _sourcePath, value))
            {
                RaiseCommandStateChanged();
            }
        }
    }

    public string DestinationDirectory
    {
        get => _destinationDirectory;
        set
        {
            if (SetField(ref _destinationDirectory, value))
            {
                RaiseCommandStateChanged();
            }
        }
    }

    public string RequestedFileName
    {
        get => _requestedFileName;
        set
        {
            if (SetField(ref _requestedFileName, value))
            {
                RaiseCommandStateChanged();
            }
        }
    }

    public SafeFileOperationKind OperationKind
    {
        get => _operationKind;
        set
        {
            if (SetField(ref _operationKind, value))
            {
                _currentPlan = null;
                RaiseDerivedPlanPropertiesChanged();
                RaiseCommandStateChanged();
            }
        }
    }

    public string PreviewText
    {
        get => _previewText;
        private set => SetField(ref _previewText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                RaiseCommandStateChanged();
            }
        }
    }

    public bool RequiresConfirmation => _currentPlan?.RequiresConfirmation == true;

    public bool WouldCreateDestinationDirectory => _currentPlan?.WouldCreateDestinationDirectory == true;

    public bool ExtensionPreserved => _currentPlan?.ExtensionPreserved == true;

    public bool PreviewIsValid => _currentPlan?.IsValid == true;

    public string DestinationPath => _currentPlan?.DestinationPath ?? string.Empty;

    public long? LastActionId
    {
        get => _lastActionId;
        private set => SetField(ref _lastActionId, value);
    }

    public long? LastUndoActionId
    {
        get => _lastUndoActionId;
        private set => SetField(ref _lastUndoActionId, value);
    }

    public Task RefreshPreviewAsync(CancellationToken cancellationToken = default)
    {
        _currentPlan = BuildPlan();
        PreviewText = FormatPreview(_currentPlan);
        StatusMessage = _currentPlan.IsValid
            ? "Preview ready. Confirmation is required before any file operation."
            : "Preview has validation errors.";

        RaiseDerivedPlanPropertiesChanged();
        RaiseCommandStateChanged();
        return Task.CompletedTask;
    }

    public async Task ConfirmOperationAsync(CancellationToken cancellationToken = default)
    {
        if (_currentPlan is null)
        {
            await RefreshPreviewAsync(cancellationToken).ConfigureAwait(true);
        }

        var plan = _currentPlan;
        if (plan is null || !plan.IsValid)
        {
            StatusMessage = "Create a valid preview before confirming.";
            return;
        }

        IsBusy = true;
        try
        {
            var fileRecord = await ResolveFileRecordAsync(cancellationToken).ConfigureAwait(true);
            if (fileRecord is null)
            {
                StatusMessage = "A matching SQLite file record is required before moving or renaming.";
                return;
            }

            var confirmed = await _confirmationService
                .ConfirmAsync("Confirm file operation", FormatConfirmationMessage(plan), cancellationToken)
                .ConfigureAwait(true);

            if (!confirmed)
            {
                LastActionId = await LogActionAsync(
                    actionType: $"{plan.OperationKind}Cancelled",
                    targetFileRecordId: fileRecord.Id,
                    oldPath: plan.SourcePath,
                    newPath: plan.DestinationPath,
                    status: "Cancelled",
                    details: new
                    {
                        plan.PlanId,
                        plan.WouldCreateDestinationDirectory,
                        reason = "User declined confirmation"
                    },
                    cancellationToken).ConfigureAwait(true);
                LastUndoActionId = null;
                StatusMessage = "Operation cancelled. No file changes were made.";
                return;
            }

            var now = _clock();
            var result = await _executor.ExecuteAsync(
                plan,
                new SafeFileOperationConfirmation(plan.PlanId, true, now),
                fileRecord.Id!.Value,
                now,
                cancellationToken).ConfigureAwait(true);

            LastActionId = result.ActionId;
            LastUndoActionId = result.UndoActionId;

            if (result.Succeeded)
            {
                StatusMessage = $"Operation completed. Undo action {result.UndoActionId} is pending.";
                SourcePath = result.DestinationPath;
                _currentPlan = null;
                PreviewText = "Operation completed. Refresh preview before another move or rename.";
                RaiseDerivedPlanPropertiesChanged();
                return;
            }

            if (result.ActionId is null)
            {
                LastActionId = await LogActionAsync(
                    actionType: $"{plan.OperationKind}Failed",
                    targetFileRecordId: fileRecord.Id,
                    oldPath: result.SourcePath,
                    newPath: result.DestinationPath,
                    status: "Failed",
                    details: new
                    {
                        plan.PlanId,
                        failure = result.FailureReason
                    },
                    cancellationToken).ConfigureAwait(true);
            }

            StatusMessage = result.FailureReason ?? "Operation failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private SafeFileOperationPlan BuildPlan()
    {
        var destinationExists = Directory.Exists(DestinationDirectory);
        var existingDestinationPaths = destinationExists
            ? Directory.EnumerateFileSystemEntries(DestinationDirectory).ToArray()
            : Array.Empty<string>();

        return _planner.Plan(new SafeFileOperationPlanRequest(
            SourcePath: SourcePath,
            DestinationDirectory: DestinationDirectory,
            RequestedFileName: string.IsNullOrWhiteSpace(RequestedFileName)
                ? Path.GetFileName(SourcePath)
                : RequestedFileName,
            OperationKind: OperationKind,
            DestinationDirectoryExists: destinationExists,
            ExistingDestinationPaths: existingDestinationPaths));
    }

    private async Task<FileRecord?> ResolveFileRecordAsync(CancellationToken cancellationToken)
    {
        if (long.TryParse(FileRecordId, out var id))
        {
            return await _store.GetFileRecordAsync(id, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(SourcePath))
        {
            return await _store.GetFileRecordByCurrentPathAsync(
                Path.GetFullPath(SourcePath),
                cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private async Task<long> LogActionAsync(
        string actionType,
        long? targetFileRecordId,
        string? oldPath,
        string? newPath,
        string status,
        object details,
        CancellationToken cancellationToken)
    {
        var now = _clock();
        return await _store.AddActionAsync(new FileActionRecord(
            Id: null,
            ActionType: actionType,
            TargetFileRecordId: targetFileRecordId,
            OldPath: oldPath,
            NewPath: newPath,
            Status: status,
            CreatedAt: now,
            CompletedAt: now,
            DetailsJson: JsonSerializer.Serialize(details)),
            cancellationToken).ConfigureAwait(false);
    }

    private static string FormatPreview(SafeFileOperationPlan plan)
    {
        var lines = new List<string>
        {
            $"Operation: {plan.OperationKind}",
            $"Source: {plan.SourcePath}",
            $"Requested name: {plan.RequestedFileName}",
            $"Sanitized name: {plan.SanitizedFileName}",
            $"Destination: {plan.DestinationPath}",
            $"Creates folder: {(plan.WouldCreateDestinationDirectory ? "yes" : "no")}",
            $"Extension preserved: {(plan.ExtensionPreserved ? "yes" : "no")}",
            $"Requires confirmation: {(plan.RequiresConfirmation ? "yes" : "no")}"
        };

        if (!plan.IsValid)
        {
            lines.Add("Validation errors:");
            lines.AddRange(plan.ValidationErrors.Select(error => $" - {error}"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatConfirmationMessage(SafeFileOperationPlan plan)
    {
        return string.Join(
            Environment.NewLine,
            $"Confirm {plan.OperationKind.ToString().ToLowerInvariant()}?",
            $"Source: {plan.SourcePath}",
            $"Destination: {plan.DestinationPath}",
            $"Creates destination folder: {(plan.WouldCreateDestinationDirectory ? "yes" : "no")}",
            "This operation will be logged and an undo record will be created.");
    }

    private bool CanConfirmOperation()
    {
        return !IsBusy && _currentPlan?.IsValid == true && _currentPlan.RequiresConfirmation;
    }

    private void RaiseDerivedPlanPropertiesChanged()
    {
        OnPropertyChanged(nameof(RequiresConfirmation));
        OnPropertyChanged(nameof(WouldCreateDestinationDirectory));
        OnPropertyChanged(nameof(ExtensionPreserved));
        OnPropertyChanged(nameof(PreviewIsValid));
        OnPropertyChanged(nameof(DestinationPath));
    }

    private void RaiseCommandStateChanged()
    {
        RefreshPreviewCommand.RaiseCanExecuteChanged();
        ConfirmOperationCommand.RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
