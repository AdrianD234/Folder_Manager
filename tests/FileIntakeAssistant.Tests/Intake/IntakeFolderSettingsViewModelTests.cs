using FileIntakeAssistant.App.ViewModels;
using FileIntakeAssistant.Core.Configuration;
using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace FileIntakeAssistant.Tests.Intake;

public sealed class IntakeFolderSettingsViewModelTests : IAsyncLifetime
{
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 23, 6, 0, 0, TimeSpan.Zero);

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
    public async Task IntakeFolders_DownloadsSuggestionStartsDisabledAndCanBeExplicitlyEnabled()
    {
        var downloads = Directory.CreateDirectory(Path.Combine(_testRoot, "Downloads")).FullName;
        var store = await CreateStoreAsync();
        var viewModel = CreateViewModel(store, _ => DownloadsSuggestion(downloads));

        await viewModel.RefreshAsync();

        var suggestion = Assert.Single(viewModel.Folders);
        Assert.Equal("Downloads", suggestion.DisplayName);
        Assert.False(suggestion.Enabled);
        Assert.Null(suggestion.Id);

        viewModel.SelectedFolder = suggestion;
        await viewModel.EnableSelectedAsync();

        var saved = Assert.Single(await store.ListIntakeFoldersAsync(enabledOnly: true));
        Assert.Equal(Path.GetFullPath(downloads).TrimEnd(Path.DirectorySeparatorChar), saved.Path);
        Assert.True(saved.Enabled);
        Assert.Equal("Downloads", saved.FolderType);
    }

    [Fact]
    public async Task IntakeFolders_ViewModelAddsDisablesEnablesAndRemovesWithoutDeletingRecord()
    {
        var intake = Directory.CreateDirectory(Path.Combine(_testRoot, "Intake")).FullName;
        var store = await CreateStoreAsync();
        var viewModel = CreateViewModel(store, _ => null);

        viewModel.NewFolderPath = intake;
        viewModel.NewDisplayName = "Temp Intake";
        viewModel.NewRecursive = true;
        await viewModel.AddAsync();

        var added = Assert.Single(await store.ListIntakeFoldersAsync(enabledOnly: false));
        Assert.True(added.Enabled);
        Assert.True(added.Recursive);
        Assert.Equal("Temp Intake", added.DisplayName);
        Assert.Single(viewModel.Folders);

        viewModel.SelectedFolder = viewModel.Folders[0];
        await viewModel.DisableSelectedAsync();

        var disabled = Assert.Single(await store.ListIntakeFoldersAsync(enabledOnly: false));
        Assert.False(disabled.Enabled);
        Assert.Empty(await store.ListIntakeFoldersAsync(enabledOnly: true));

        viewModel.SelectedFolder = viewModel.Folders[0];
        await viewModel.EnableSelectedAsync();

        var enabled = Assert.Single(await store.ListIntakeFoldersAsync(enabledOnly: true));
        Assert.True(enabled.Enabled);

        viewModel.SelectedFolder = viewModel.Folders[0];
        await viewModel.RemoveSelectedAsync();

        var removed = Assert.Single(await store.ListIntakeFoldersAsync(enabledOnly: false));
        Assert.False(removed.Enabled);
        Assert.Contains("Removed", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IntakeFolders_BroadRootsAndRepoMarkersAreRejectedBeforeWatcherConstruction()
    {
        var store = await CreateStoreAsync();
        var service = new IntakeFolderSettingsService(
            store,
            new IntakeFolderPathValidator(),
            new IntakeFolderPathValidationOptions(
                UserProfilePath: Path.Combine(_testRoot, "User"),
                AppDataPath: Path.Combine(_testRoot, "User", "AppData", "Roaming"),
                LocalAppDataPath: Path.Combine(_testRoot, "User", "AppData", "Local"),
                ProgramFilesPath: Path.Combine(_testRoot, "Program Files"),
                ProgramFilesX86Path: Path.Combine(_testRoot, "Program Files (x86)"),
                WindowsPath: Path.Combine(_testRoot, "Windows")));

        var userProfile = Directory.CreateDirectory(Path.Combine(_testRoot, "User")).FullName;
        var appData = Directory.CreateDirectory(Path.Combine(_testRoot, "User", "AppData", "Local", "Vendor")).FullName;
        var repo = Directory.CreateDirectory(Path.Combine(_testRoot, "Repo")).FullName;
        Directory.CreateDirectory(Path.Combine(repo, ".git"));

        var userProfileResult = await service.AddOrUpdateAsync(
            Request(userProfile, directoryExists: true, containsRepositoryMarker: false),
            FixedNow);
        var appDataResult = await service.AddOrUpdateAsync(
            Request(appData, directoryExists: true, containsRepositoryMarker: false),
            FixedNow);
        var repoResult = await service.AddOrUpdateAsync(
            Request(repo, directoryExists: true, containsRepositoryMarker: true),
            FixedNow);

        Assert.False(userProfileResult.Succeeded);
        Assert.False(appDataResult.Succeeded);
        Assert.False(repoResult.Succeeded);
        Assert.Empty(await store.ListIntakeFoldersAsync(enabledOnly: false));
    }

    private async Task<SqliteFileIntakeStore> CreateStoreAsync()
    {
        await new SqliteMigrationRunner().ApplyMigrationsAsync(DatabasePath);
        return new SqliteFileIntakeStore(DatabasePath);
    }

    private IntakeFolderSettingsViewModel CreateViewModel(
        SqliteFileIntakeStore store,
        Func<DateTimeOffset, IntakeFolder?> suggestionFactory)
    {
        return new IntakeFolderSettingsViewModel(
            new IntakeFolderSettingsService(
                store,
                new IntakeFolderPathValidator(),
                new IntakeFolderPathValidationOptions(
                    UserProfilePath: Path.Combine(_testRoot, "User"),
                    AppDataPath: Path.Combine(_testRoot, "User", "AppData", "Roaming"),
                    LocalAppDataPath: Path.Combine(_testRoot, "User", "AppData", "Local"),
                    ProgramFilesPath: Path.Combine(_testRoot, "Program Files"),
                    ProgramFilesX86Path: Path.Combine(_testRoot, "Program Files (x86)"),
                    WindowsPath: Path.Combine(_testRoot, "Windows"))),
            suggestionFactory,
            Directory.Exists,
            path => Directory.Exists(Path.Combine(path, ".git")),
            () => FixedNow);
    }

    private static IntakeFolderSettingsRequest Request(
        string path,
        bool directoryExists,
        bool containsRepositoryMarker)
    {
        return new IntakeFolderSettingsRequest(
            path,
            DisplayName: null,
            FolderType: "Intake",
            Enabled: true,
            Recursive: false,
            directoryExists,
            containsRepositoryMarker);
    }

    private static IntakeFolder DownloadsSuggestion(string path)
    {
        return new IntakeFolder(
            Id: null,
            Path: path,
            DisplayName: "Downloads",
            Enabled: false,
            FolderType: "Downloads",
            Recursive: false,
            CreatedAt: FixedNow,
            UpdatedAt: FixedNow);
    }
}
