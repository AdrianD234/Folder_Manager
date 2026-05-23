using FileIntakeAssistant.App.ViewModels;
using FileIntakeAssistant.Core.FileOperations;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;
using FileIntakeAssistant.Infrastructure.FileSystem;
using FileIntakeAssistant.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace FileIntakeAssistant.Tests.FileOperations;

public sealed class SafeFileOperationViewModelTests : IAsyncLifetime
{
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 23, 5, 0, 0, TimeSpan.Zero);

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
    public async Task SafeFileOperations_PreviewShowsConfirmationStateAndDestination()
    {
        var store = await CreateStoreAsync();
        var source = CreateTempFile("downloads", "Budget.xlsx", "budget-v1");
        var fileRecordId = await AddFileRecordAsync(store, source);
        var destination = Path.Combine(_testRoot, "filed");
        Directory.CreateDirectory(destination);
        await File.WriteAllTextAsync(Path.Combine(destination, "Budget.xlsx"), "existing");
        await File.WriteAllTextAsync(Path.Combine(destination, "Budget (2).xlsx"), "existing");
        var viewModel = CreateOperationViewModel(store, userConfirms: false);

        viewModel.FileRecordId = fileRecordId.ToString();
        viewModel.SourcePath = source;
        viewModel.DestinationDirectory = destination;
        viewModel.RequestedFileName = "Budget.xlsx";

        await viewModel.RefreshPreviewAsync();

        Assert.True(viewModel.PreviewIsValid);
        Assert.True(viewModel.RequiresConfirmation);
        Assert.True(viewModel.ExtensionPreserved);
        Assert.False(viewModel.WouldCreateDestinationDirectory);
        Assert.EndsWith("Budget (3).xlsx", viewModel.DestinationPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Source:", viewModel.PreviewText);
        Assert.Contains("Destination:", viewModel.PreviewText);
    }

    [Fact]
    public async Task SafeFileOperations_ConfirmationRefusalDoesNotMutateFilesystem()
    {
        var store = await CreateStoreAsync();
        var source = CreateTempFile("downloads", "Budget.xlsx", "budget-v1");
        var fileRecordId = await AddFileRecordAsync(store, source);
        var destination = Path.Combine(_testRoot, "new-filed-folder");
        var viewModel = CreateOperationViewModel(store, userConfirms: false);

        viewModel.FileRecordId = fileRecordId.ToString();
        viewModel.SourcePath = source;
        viewModel.DestinationDirectory = destination;
        viewModel.RequestedFileName = "Budget filed.xlsx";

        await viewModel.RefreshPreviewAsync();
        await viewModel.ConfirmOperationAsync();

        Assert.True(File.Exists(source));
        Assert.False(Directory.Exists(destination));
        Assert.Null(viewModel.LastUndoActionId);
        Assert.NotNull(viewModel.LastActionId);

        var action = await store.GetActionAsync(viewModel.LastActionId!.Value);
        Assert.Equal("MoveCancelled", action!.ActionType);
        Assert.Equal("Cancelled", action.Status);
    }

    [Fact]
    public async Task SafeFileOperations_ConfirmedViewModelMoveUsesSafeExecutorAndCreatesUndo()
    {
        var store = await CreateStoreAsync();
        var source = CreateTempFile("downloads", "Budget.xlsx", "budget-v1");
        var fileRecordId = await AddFileRecordAsync(store, source);
        var destination = Path.Combine(_testRoot, "filed");
        var viewModel = CreateOperationViewModel(store, userConfirms: true);

        viewModel.FileRecordId = fileRecordId.ToString();
        viewModel.SourcePath = source;
        viewModel.DestinationDirectory = destination;
        viewModel.RequestedFileName = "Budget filed.xlsx";

        await viewModel.RefreshPreviewAsync();
        await viewModel.ConfirmOperationAsync();

        Assert.False(File.Exists(source));
        Assert.True(File.Exists(Path.Combine(destination, "Budget filed.xlsx")));
        Assert.NotNull(viewModel.LastActionId);
        Assert.NotNull(viewModel.LastUndoActionId);

        var action = await store.GetActionAsync(viewModel.LastActionId!.Value);
        var undo = await store.GetUndoActionAsync(viewModel.LastUndoActionId!.Value);
        Assert.Equal("Move", action!.ActionType);
        Assert.Equal("Completed", action.Status);
        Assert.Equal("Pending", undo!.Status);
    }

    [Fact]
    public async Task Undo_SuccessFromViewModelRestoresFileAndLogsAuditAction()
    {
        var store = await CreateStoreAsync();
        var source = CreateTempFile("downloads", "Budget.xlsx", "budget-v1");
        var destination = Path.Combine(_testRoot, "filed");
        Directory.CreateDirectory(destination);
        var move = await MoveFileAsync(store, source, destination);
        var viewModel = CreateUndoViewModel(store, userConfirms: true);

        await viewModel.RefreshAsync();
        viewModel.SelectedUndoAction = viewModel.UndoActions.Single(action => action.Id == move.UndoActionId);
        await viewModel.UndoSelectedAsync();

        Assert.True(File.Exists(source));
        Assert.False(File.Exists(Path.Combine(destination, "Budget.xlsx")));
        Assert.NotNull(viewModel.LastAuditActionId);

        var auditAction = await store.GetActionAsync(viewModel.LastAuditActionId!.Value);
        var undo = await store.GetUndoActionAsync(move.UndoActionId!.Value);
        Assert.Equal("UndoPerformed", auditAction!.ActionType);
        Assert.Equal("Completed", auditAction.Status);
        Assert.Equal("Performed", undo!.Status);
    }

    [Fact]
    public async Task Undo_ConflictFromViewModelFailsSafelyWithoutOverwrite()
    {
        var store = await CreateStoreAsync();
        var source = CreateTempFile("downloads", "Budget.xlsx", "budget-v1");
        var destination = Path.Combine(_testRoot, "filed");
        Directory.CreateDirectory(destination);
        var move = await MoveFileAsync(store, source, destination);
        await File.WriteAllTextAsync(source, "occupied");
        var viewModel = CreateUndoViewModel(store, userConfirms: true);

        await viewModel.RefreshAsync();
        viewModel.SelectedUndoAction = viewModel.UndoActions.Single(action => action.Id == move.UndoActionId);
        await viewModel.UndoSelectedAsync();

        Assert.Equal("occupied", await File.ReadAllTextAsync(source));
        Assert.True(File.Exists(Path.Combine(destination, "Budget.xlsx")));

        var auditAction = await store.GetActionAsync(viewModel.LastAuditActionId!.Value);
        var undo = await store.GetUndoActionAsync(move.UndoActionId!.Value);
        Assert.Equal("UndoFailed", auditAction!.ActionType);
        Assert.Equal("Failed", auditAction.Status);
        Assert.Equal("Failed", undo!.Status);
    }

    [Fact]
    public async Task Undo_IdentityMismatchFromViewModelFailsSafely()
    {
        var store = await CreateStoreAsync();
        var source = CreateTempFile("downloads", "Budget.xlsx", "budget-v1");
        var destination = Path.Combine(_testRoot, "filed");
        Directory.CreateDirectory(destination);
        var move = await MoveFileAsync(store, source, destination);
        var movedPath = Path.Combine(destination, "Budget.xlsx");
        await File.WriteAllTextAsync(movedPath, "modified");
        File.SetLastWriteTimeUtc(movedPath, DateTime.UtcNow.AddMinutes(5));
        var viewModel = CreateUndoViewModel(store, userConfirms: true);

        await viewModel.RefreshAsync();
        viewModel.SelectedUndoAction = viewModel.UndoActions.Single(action => action.Id == move.UndoActionId);
        await viewModel.UndoSelectedAsync();

        Assert.False(File.Exists(source));
        Assert.True(File.Exists(movedPath));

        var auditAction = await store.GetActionAsync(viewModel.LastAuditActionId!.Value);
        var undo = await store.GetUndoActionAsync(move.UndoActionId!.Value);
        Assert.Equal("UndoFailed", auditAction!.ActionType);
        Assert.Equal("Failed", auditAction.Status);
        Assert.Equal("Failed", undo!.Status);
    }

    private async Task<SqliteFileIntakeStore> CreateStoreAsync()
    {
        Assert.StartsWith(
            Path.GetFullPath(Path.GetTempPath()),
            Path.GetFullPath(DatabasePath),
            StringComparison.OrdinalIgnoreCase);

        var migrationRunner = new SqliteMigrationRunner();
        await migrationRunner.ApplyMigrationsAsync(DatabasePath);
        return new SqliteFileIntakeStore(DatabasePath);
    }

    private SafeFileOperationViewModel CreateOperationViewModel(
        IFileIntakeStore store,
        bool userConfirms)
    {
        return new SafeFileOperationViewModel(
            store,
            new SafeFileOperationExecutor(store),
            new FakeConfirmationService(userConfirms),
            () => FixedNow);
    }

    private UndoActionsViewModel CreateUndoViewModel(
        IFileIntakeStore store,
        bool userConfirms)
    {
        return new UndoActionsViewModel(
            store,
            new SafeFileOperationExecutor(store),
            new FakeConfirmationService(userConfirms),
            () => FixedNow.AddMinutes(5));
    }

    private async Task<SafeFileOperationExecutionResult> MoveFileAsync(
        IFileIntakeStore store,
        string source,
        string destination)
    {
        var fileRecordId = await AddFileRecordAsync(store, source);
        var plan = new SafeFileOperationPlanner().Plan(new SafeFileOperationPlanRequest(
            SourcePath: source,
            DestinationDirectory: destination,
            RequestedFileName: Path.GetFileName(source),
            OperationKind: SafeFileOperationKind.Move,
            DestinationDirectoryExists: Directory.Exists(destination),
            ExistingDestinationPaths: Directory.EnumerateFileSystemEntries(destination).ToArray()));

        var executor = new SafeFileOperationExecutor(store);
        return await executor.ExecuteAsync(
            plan,
            new SafeFileOperationConfirmation(plan.PlanId, true, FixedNow.AddMinutes(1)),
            fileRecordId,
            FixedNow.AddMinutes(1));
    }

    private string CreateTempFile(string folderName, string fileName, string content)
    {
        var folder = Path.Combine(_testRoot, folderName);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, fileName);
        File.WriteAllText(path, content);
        return path;
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

    private sealed class FakeConfirmationService : IUserConfirmationService
    {
        private readonly bool _confirmed;

        public FakeConfirmationService(bool confirmed)
        {
            _confirmed = confirmed;
        }

        public Task<bool> ConfirmAsync(
            string title,
            string message,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_confirmed);
        }
    }
}
