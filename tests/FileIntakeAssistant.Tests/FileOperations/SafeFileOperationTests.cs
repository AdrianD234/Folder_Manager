using FileIntakeAssistant.Core.FileOperations;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;
using FileIntakeAssistant.Infrastructure.FileSystem;
using FileIntakeAssistant.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace FileIntakeAssistant.Tests.FileOperations;

public sealed class SafeFileOperationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 23, 4, 0, 0, TimeSpan.Zero);

    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "FileIntakeAssistant.Tests",
        Guid.NewGuid().ToString("N"));

    private string DatabasePath => Path.Combine(_testRoot, "File Intake Assistant", "data", "file-intake-test.db");

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        var fullRoot = Path.GetFullPath(_testRoot);
        var allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "FileIntakeAssistant.Tests"));

        if (fullRoot.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullRoot))
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(fullRoot, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public void SafeFileOperations_SanitizesIllegalCharactersAndReservedNames()
    {
        var sanitizer = new FileNameSanitizer();

        var sanitized = sanitizer.Sanitize("Budget:Q2?draft.pdf", ".xlsx");
        var reserved = sanitizer.Sanitize("CON.txt", ".txt");

        Assert.Equal("Budget_Q2_draft.xlsx", sanitized);
        Assert.Equal("CON_.txt", reserved);
    }

    [Fact]
    public void SafeFileOperations_PreservesExtensionUnlessExplicitlyAllowed()
    {
        var planner = new SafeFileOperationPlanner();
        var source = Path.Combine(_testRoot, "downloads", "Budget.xlsx");
        var destination = Path.Combine(_testRoot, "filed");

        var preserved = planner.Plan(new SafeFileOperationPlanRequest(
            SourcePath: source,
            DestinationDirectory: destination,
            RequestedFileName: "Budget final.pdf",
            OperationKind: SafeFileOperationKind.Rename,
            DestinationDirectoryExists: true,
            ExistingDestinationPaths: Array.Empty<string>()));

        var changed = planner.Plan(new SafeFileOperationPlanRequest(
            SourcePath: source,
            DestinationDirectory: destination,
            RequestedFileName: "Budget final.pdf",
            OperationKind: SafeFileOperationKind.Rename,
            DestinationDirectoryExists: true,
            ExistingDestinationPaths: Array.Empty<string>(),
            AllowExtensionChange: true));

        Assert.Equal(".xlsx", Path.GetExtension(preserved.DestinationPath));
        Assert.True(preserved.ExtensionPreserved);
        Assert.Equal(".pdf", Path.GetExtension(changed.DestinationPath));
        Assert.False(changed.ExtensionPreserved);
    }

    [Fact]
    public void SafeFileOperations_ConflictResolutionUsesNumberedNonOverwritingCandidates()
    {
        var planner = new SafeFileOperationPlanner();
        var source = Path.Combine(_testRoot, "downloads", "Budget.xlsx");
        var destination = Path.Combine(_testRoot, "filed");
        var existing = new[]
        {
            Path.Combine(destination, "Budget.xlsx"),
            Path.Combine(destination, "Budget (2).xlsx")
        };

        var plan = planner.Plan(new SafeFileOperationPlanRequest(
            SourcePath: source,
            DestinationDirectory: destination,
            RequestedFileName: "Budget.xlsx",
            OperationKind: SafeFileOperationKind.Move,
            DestinationDirectoryExists: true,
            ExistingDestinationPaths: existing));

        Assert.True(plan.IsValid);
        Assert.Equal(Path.Combine(destination, "Budget (3).xlsx"), plan.DestinationPath);
    }

    [Fact]
    public void SafeFileOperations_DestinationValidationRejectsNoOpPath()
    {
        var planner = new SafeFileOperationPlanner();
        var source = Path.Combine(_testRoot, "downloads", "Budget.xlsx");

        var plan = planner.Plan(new SafeFileOperationPlanRequest(
            SourcePath: source,
            DestinationDirectory: Path.GetDirectoryName(source)!,
            RequestedFileName: "Budget.xlsx",
            OperationKind: SafeFileOperationKind.Rename,
            DestinationDirectoryExists: true,
            ExistingDestinationPaths: Array.Empty<string>()));

        Assert.False(plan.IsValid);
        Assert.Contains(plan.ValidationErrors, error => error.Contains("must differ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SafeFileOperations_MoveWithTempFilesRecordsActionAndUndoRows()
    {
        var store = await CreateStoreAsync();
        var source = CreateTempFile("downloads", "Budget.xlsx", "budget-v1");
        var fileRecordId = await AddFileRecordAsync(store, source);
        var destination = Path.Combine(_testRoot, "filed");
        Directory.CreateDirectory(destination);
        var plan = PlanMove(source, destination, "Budget.xlsx");

        var executor = new SafeFileOperationExecutor(store);
        var result = await executor.ExecuteAsync(
            plan,
            Confirm(plan),
            fileRecordId,
            FixedNow.AddMinutes(1));

        Assert.True(result.Succeeded, result.FailureReason);
        Assert.False(File.Exists(source));
        Assert.True(File.Exists(plan.DestinationPath));
        Assert.Equal("budget-v1", await File.ReadAllTextAsync(plan.DestinationPath));

        var action = await store.GetActionAsync(result.ActionId!.Value);
        var undo = await store.GetUndoActionAsync(result.UndoActionId!.Value);
        var fileRecord = await store.GetFileRecordAsync(fileRecordId);

        Assert.Equal("Completed", action!.Status);
        Assert.Equal("Move", action.ActionType);
        Assert.Equal("Pending", undo!.Status);
        Assert.Equal(source, undo.OriginalPath);
        Assert.Equal(plan.DestinationPath, undo.ResultingPath);
        Assert.Equal(plan.DestinationPath, fileRecord!.CurrentPath);
        Assert.Equal("Filed", fileRecord.Status);
    }

    [Fact]
    public async Task SafeFileOperations_ExecutorRefusesStaleDestinationConflictWithoutOverwrite()
    {
        var store = await CreateStoreAsync();
        var source = CreateTempFile("downloads", "Budget.xlsx", "budget-v1");
        var fileRecordId = await AddFileRecordAsync(store, source);
        var destination = Path.Combine(_testRoot, "filed");
        Directory.CreateDirectory(destination);
        var plan = PlanMove(source, destination, "Budget.xlsx");
        await File.WriteAllTextAsync(plan.DestinationPath, "existing");

        var executor = new SafeFileOperationExecutor(store);
        var result = await executor.ExecuteAsync(
            plan,
            Confirm(plan),
            fileRecordId,
            FixedNow.AddMinutes(1));

        Assert.False(result.Succeeded);
        Assert.Contains("already exists", result.FailureReason!, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(source));
        Assert.Equal("existing", await File.ReadAllTextAsync(plan.DestinationPath));
        Assert.Null(result.ActionId);
        Assert.Null(result.UndoActionId);
    }

    [Fact]
    public async Task SafeFileOperations_RenameWithTempFilesRecordsActionAndUndoRows()
    {
        var store = await CreateStoreAsync();
        var source = CreateTempFile("downloads", "Budget.xlsx", "budget-v1");
        var fileRecordId = await AddFileRecordAsync(store, source);
        var plan = PlanMove(source, Path.GetDirectoryName(source)!, "Budget final.xlsx", SafeFileOperationKind.Rename);

        var executor = new SafeFileOperationExecutor(store);
        var result = await executor.ExecuteAsync(
            plan,
            Confirm(plan),
            fileRecordId,
            FixedNow.AddMinutes(1));

        Assert.True(result.Succeeded, result.FailureReason);
        Assert.False(File.Exists(source));
        Assert.True(File.Exists(plan.DestinationPath));
        Assert.Equal(Path.Combine(Path.GetDirectoryName(source)!, "Budget final.xlsx"), plan.DestinationPath);

        var action = await store.GetActionAsync(result.ActionId!.Value);
        var undo = await store.GetUndoActionAsync(result.UndoActionId!.Value);

        Assert.Equal("Rename", action!.ActionType);
        Assert.Equal("Completed", action.Status);
        Assert.Equal("RenameBack", undo!.UndoType);
        Assert.Equal("Pending", undo.Status);
    }

    [Fact]
    public async Task Undo_RestoresMovedFileWhenIdentityMatchesAndOriginalPathIsFree()
    {
        var store = await CreateStoreAsync();
        var source = CreateTempFile("downloads", "Budget.xlsx", "budget-v1");
        var fileRecordId = await AddFileRecordAsync(store, source);
        var destination = Path.Combine(_testRoot, "filed");
        Directory.CreateDirectory(destination);
        var plan = PlanMove(source, destination, "Budget.xlsx");
        var executor = new SafeFileOperationExecutor(store);

        var move = await executor.ExecuteAsync(plan, Confirm(plan), fileRecordId, FixedNow.AddMinutes(1));
        var undo = await executor.UndoAsync(move.UndoActionId!.Value, FixedNow.AddMinutes(2));

        Assert.True(undo.Succeeded, undo.FailureReason);
        Assert.True(File.Exists(source));
        Assert.False(File.Exists(plan.DestinationPath));
        Assert.Equal("budget-v1", await File.ReadAllTextAsync(source));

        var undoRecord = await store.GetUndoActionAsync(move.UndoActionId.Value);
        var fileRecord = await store.GetFileRecordAsync(fileRecordId);

        Assert.Equal("Performed", undoRecord!.Status);
        Assert.Equal(source, fileRecord!.CurrentPath);
        Assert.Equal("Candidate", fileRecord.Status);
    }

    [Fact]
    public async Task Undo_FailsSafelyWhenOriginalPathIsOccupied()
    {
        var store = await CreateStoreAsync();
        var source = CreateTempFile("downloads", "Budget.xlsx", "budget-v1");
        var fileRecordId = await AddFileRecordAsync(store, source);
        var destination = Path.Combine(_testRoot, "filed");
        Directory.CreateDirectory(destination);
        var plan = PlanMove(source, destination, "Budget.xlsx");
        var executor = new SafeFileOperationExecutor(store);

        var move = await executor.ExecuteAsync(plan, Confirm(plan), fileRecordId, FixedNow.AddMinutes(1));
        await File.WriteAllTextAsync(source, "conflict");

        var undo = await executor.UndoAsync(move.UndoActionId!.Value, FixedNow.AddMinutes(2));

        Assert.False(undo.Succeeded);
        Assert.Contains("occupied", undo.FailureReason!, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(source));
        Assert.True(File.Exists(plan.DestinationPath));
        Assert.Equal("conflict", await File.ReadAllTextAsync(source));

        var undoRecord = await store.GetUndoActionAsync(move.UndoActionId.Value);
        Assert.Equal("Failed", undoRecord!.Status);
    }

    [Fact]
    public async Task Undo_FailsSafelyWhenCurrentFileIdentityDoesNotMatch()
    {
        var store = await CreateStoreAsync();
        var source = CreateTempFile("downloads", "Budget.xlsx", "budget-v1");
        var fileRecordId = await AddFileRecordAsync(store, source);
        var destination = Path.Combine(_testRoot, "filed");
        Directory.CreateDirectory(destination);
        var plan = PlanMove(source, destination, "Budget.xlsx");
        var executor = new SafeFileOperationExecutor(store);

        var move = await executor.ExecuteAsync(plan, Confirm(plan), fileRecordId, FixedNow.AddMinutes(1));
        await File.WriteAllTextAsync(plan.DestinationPath, "modified");
        File.SetLastWriteTimeUtc(plan.DestinationPath, DateTime.UtcNow.AddMinutes(5));

        var undo = await executor.UndoAsync(move.UndoActionId!.Value, FixedNow.AddMinutes(2));

        Assert.False(undo.Succeeded);
        Assert.Contains("identity", undo.FailureReason!, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(source));
        Assert.True(File.Exists(plan.DestinationPath));

        var undoRecord = await store.GetUndoActionAsync(move.UndoActionId.Value);
        Assert.Equal("Failed", undoRecord!.Status);
    }

    [Fact]
    public async Task SafeFileOperations_UnconfirmedMoveDoesNotCreateDestinationFolder()
    {
        var store = await CreateStoreAsync();
        var source = CreateTempFile("downloads", "Budget.xlsx", "budget-v1");
        var fileRecordId = await AddFileRecordAsync(store, source);
        var destination = Path.Combine(_testRoot, "new-filed-folder");
        var plan = PlanMove(source, destination, "Budget.xlsx");
        var executor = new SafeFileOperationExecutor(store);

        var result = await executor.ExecuteAsync(
            plan,
            new SafeFileOperationConfirmation(plan.PlanId, false, FixedNow.AddMinutes(1)),
            fileRecordId,
            FixedNow.AddMinutes(1));

        Assert.False(result.Succeeded);
        Assert.Contains("confirmed", result.FailureReason!, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(source));
        Assert.False(Directory.Exists(destination));
        Assert.Null(result.ActionId);
        Assert.Null(result.UndoActionId);
    }

    private async Task<IFileIntakeStore> CreateStoreAsync()
    {
        Assert.StartsWith(
            Path.GetFullPath(Path.GetTempPath()),
            Path.GetFullPath(DatabasePath),
            StringComparison.OrdinalIgnoreCase);

        var migrationRunner = new SqliteMigrationRunner();
        await migrationRunner.ApplyMigrationsAsync(DatabasePath);
        return new SqliteFileIntakeStore(DatabasePath);
    }

    private string CreateTempFile(string folderName, string fileName, string content)
    {
        var folder = Path.Combine(_testRoot, folderName);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static SafeFileOperationPlan PlanMove(
        string source,
        string destination,
        string requestedFileName,
        SafeFileOperationKind operationKind = SafeFileOperationKind.Move)
    {
        var existing = Directory.Exists(destination)
            ? Directory.EnumerateFileSystemEntries(destination).ToArray()
            : Array.Empty<string>();

        return new SafeFileOperationPlanner().Plan(new SafeFileOperationPlanRequest(
            SourcePath: source,
            DestinationDirectory: destination,
            RequestedFileName: requestedFileName,
            OperationKind: operationKind,
            DestinationDirectoryExists: Directory.Exists(destination),
            ExistingDestinationPaths: existing));
    }

    private static SafeFileOperationConfirmation Confirm(SafeFileOperationPlan plan)
    {
        return new SafeFileOperationConfirmation(plan.PlanId, true, FixedNow.AddMinutes(1));
    }

    private static async Task<long> AddFileRecordAsync(IFileIntakeStore store, string source)
    {
        var info = new FileInfo(source);

        return await store.AddFileRecordAsync(new FileRecord(
            Id: null,
            Sha256: null,
            OriginalFilename: Path.GetFileName(source),
            CurrentFilename: Path.GetFileName(source),
            OriginalPath: source,
            CurrentPath: source,
            Extension: Path.GetExtension(source),
            SizeBytes: info.Length,
            MimeType: null,
            SourceIntakeFolderId: null,
            FirstSeenAt: FixedNow,
            LastSeenAt: FixedNow,
            StableAt: FixedNow,
            Status: "Candidate",
            TriageCategory: "MeaningfulOneOff",
            TriageConfidence: 0.95,
            IsMeaningful: true,
            NotesJson: null));
    }
}
