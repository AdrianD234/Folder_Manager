using System.IO;
using System.Windows;
using FileIntakeAssistant.App.Shell;
using FileIntakeAssistant.App.ViewModels;
using FileIntakeAssistant.Core.Batching;
using FileIntakeAssistant.Core.Configuration;
using FileIntakeAssistant.Core.Intake;
using FileIntakeAssistant.Core.Metadata;
using FileIntakeAssistant.Core.Search;
using FileIntakeAssistant.Core.Stability;
using FileIntakeAssistant.Core.Transcription;
using FileIntakeAssistant.Core.Triage;
using FileIntakeAssistant.Infrastructure.Configuration;
using FileIntakeAssistant.Infrastructure.FileSystem;
using FileIntakeAssistant.Infrastructure.Logging;
using FileIntakeAssistant.Infrastructure.Persistence;
using FileIntakeAssistant.Infrastructure.Search;

namespace FileIntakeAssistant.App;

public partial class App : System.Windows.Application
{
    private TrayIconController? _trayIcon;
    private GlobalHotkeyController? _globalHotkey;
    private IntakeWatcherCoordinator? _watcherCoordinator;
    private MainWindow? _mainWindow;
    private AppLifecycleAudit? _lifecycleAudit;
    private bool _intakePopupOpen;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            var paths = FileIntakeAppDataPathProvider.GetDefault();
            Directory.CreateDirectory(paths.DataDirectory);
            Directory.CreateDirectory(paths.LogsDirectory);

            var localAuditLog = new JsonLinesLocalAuditLog(paths.LogsDirectory);
            _lifecycleAudit = new AppLifecycleAudit(localAuditLog);
            _lifecycleAudit.WriteFireAndForget(
                "app.startup",
                "Started",
                new Dictionary<string, object?>
                {
                    ["component"] = "app"
                });

            var migrationRunner = new SqliteMigrationRunner();
            migrationRunner.ApplyMigrationsAsync(paths.DatabasePath).GetAwaiter().GetResult();

            var store = new StructuredLoggingFileIntakeStore(
                new SqliteFileIntakeStore(paths.DatabasePath),
                localAuditLog);
            var candidateQueue = new InMemoryIntakeCandidateQueue();
            var manualSnapshotReader = new LocalManualFileSnapshotReader();
            var manualMetadataCaptureService = new ManualMetadataCaptureService(store);
            var transcriptionWorkflowService = new TranscriptionWorkflowService(store);
            var intakeCandidateWorkflowService = new IntakeCandidateWorkflowService(
                manualSnapshotReader,
                manualMetadataCaptureService,
                transcriptionWorkflowService,
                store);
            var manualViewModel = new ManualMetadataCaptureViewModel(
                manualSnapshotReader,
                manualMetadataCaptureService);
            var sqliteSearchProvider = new SqliteSearchProvider(paths.DatabasePath);
            var searchProvider = new CompositeFileSearchProvider(
                sqliteSearchProvider,
                new EverythingCliSearchProvider());
            var confirmationService = new MessageBoxUserConfirmationService();
            var fileLaunchService = new WindowsFileLaunchService();
            var safeFileOperationExecutor = new SafeFileOperationExecutor(store);
            var searchViewModel = new SearchCommandViewModel(
                new SearchWorkflowService(
                    new SearchIntentParser(),
                    searchProvider,
                    store),
                fileLaunchService,
                confirmationService,
                store);
            var intakeFoldersViewModel = new IntakeFolderSettingsViewModel(
                new IntakeFolderSettingsService(
                    store,
                    new IntakeFolderPathValidator(),
                    IntakeFolderPathValidationOptions.FromCurrentEnvironment()),
                DefaultIntakeFolderProvider.CreateDownloadsSuggestion);
            var candidatesViewModel = new IntakeCandidateQueueViewModel(candidateQueue, store);
            var filingViewModel = new SafeFileOperationViewModel(
                store,
                safeFileOperationExecutor,
                confirmationService);
            var undoActionsViewModel = new UndoActionsViewModel(
                store,
                safeFileOperationExecutor,
                confirmationService);
            var viewModel = new MainViewModel(
                manualViewModel,
                searchViewModel,
                intakeFoldersViewModel,
                candidatesViewModel,
                filingViewModel,
                undoActionsViewModel);

            intakeFoldersViewModel.RefreshAsync().GetAwaiter().GetResult();
            candidatesViewModel.RefreshAsync().GetAwaiter().GetResult();
            undoActionsViewModel.RefreshAsync().GetAwaiter().GetResult();

            var window = new MainWindow(viewModel);
            _mainWindow = window;
            MainWindow = window;
            _trayIcon = new TrayIconController(
                window,
                window.ShowIntake,
                window.ShowSearch,
                ExitApplication,
                _lifecycleAudit);
            _globalHotkey = new GlobalHotkeyController(window, window.ShowSearch, _lifecycleAudit);
            _watcherCoordinator = new IntakeWatcherCoordinator(
                store,
                new AuditedIntakeEventProcessor(
                    new IntakeEventProcessor(new FileEventTriageEngine(), candidateQueue),
                    store),
                new LocalFileStabilitySnapshotReader(),
                new FileStabilityChecker(),
                new BatchDetector(),
                afterProcessedAsync: () => Dispatcher
                    .InvokeAsync(async () =>
                    {
                        await candidatesViewModel.RefreshAsync().ConfigureAwait(true);
                        ShowNextIntakePopup(candidateQueue, intakeCandidateWorkflowService, candidatesViewModel);
                    })
                    .Task
                    .Unwrap(),
                audit: _lifecycleAudit);
            intakeFoldersViewModel.FoldersChanged += OnIntakeFoldersChanged;
            _watcherCoordinator.RestartAsync().GetAwaiter().GetResult();
            window.Show();
            _lifecycleAudit.WriteFireAndForget(
                "app.startup",
                "Completed",
                new Dictionary<string, object?>
                {
                    ["component"] = "app"
                });
        }
        catch (Exception ex)
        {
            _lifecycleAudit?.WriteAsync(
                "app.startup",
                "Failed",
                new Dictionary<string, object?>
                {
                    ["component"] = "app",
                    ["errorType"] = ex.GetType().Name
                }).GetAwaiter().GetResult();
            System.Windows.MessageBox.Show(
                ex.Message,
                "File Intake Assistant",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _lifecycleAudit?.WriteAsync(
            "app.exit",
            "Started",
            new Dictionary<string, object?>
            {
                ["component"] = "app"
            }).GetAwaiter().GetResult();
        _watcherCoordinator?.Dispose();
        _globalHotkey?.Dispose();
        _trayIcon?.Dispose();
        _lifecycleAudit?.WriteAsync(
            "app.exit",
            "Completed",
            new Dictionary<string, object?>
            {
                ["component"] = "app"
            }).GetAwaiter().GetResult();
        base.OnExit(e);
    }

    private void ExitApplication()
    {
        _mainWindow?.AllowClose();
        Shutdown();
    }

    private async void OnIntakeFoldersChanged(object? sender, EventArgs e)
    {
        if (_watcherCoordinator is not null)
        {
            await _watcherCoordinator.RestartAsync().ConfigureAwait(true);
        }
    }

    private void ShowNextIntakePopup(
        IIntakeCandidateQueue candidateQueue,
        IntakeCandidateWorkflowService workflowService,
        IntakeCandidateQueueViewModel candidatesViewModel)
    {
        if (_intakePopupOpen || _mainWindow is null)
        {
            return;
        }

        if (!candidateQueue.TryDequeue(out var candidate) || candidate is null)
        {
            return;
        }

        var viewModel = new IntakeCandidatePopupViewModel(candidate, workflowService);
        var popup = new IntakePopupWindow(viewModel)
        {
            Owner = _mainWindow
        };

        _intakePopupOpen = true;
        popup.Closed += async (_, _) =>
        {
            _intakePopupOpen = false;
            await candidatesViewModel.RefreshAsync().ConfigureAwait(true);
            ShowNextIntakePopup(candidateQueue, workflowService, candidatesViewModel);
        };
        popup.Show();
        popup.Activate();
    }
}
