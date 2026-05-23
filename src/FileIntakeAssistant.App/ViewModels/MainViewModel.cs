namespace FileIntakeAssistant.App.ViewModels;

public sealed class MainViewModel
{
    public MainViewModel(
        ManualMetadataCaptureViewModel manual,
        SearchCommandViewModel search,
        IntakeFolderSettingsViewModel intakeFolders,
        IntakeCandidateQueueViewModel candidates,
        SafeFileOperationViewModel filing,
        UndoActionsViewModel undoActions)
    {
        Manual = manual ?? throw new ArgumentNullException(nameof(manual));
        Search = search ?? throw new ArgumentNullException(nameof(search));
        IntakeFolders = intakeFolders ?? throw new ArgumentNullException(nameof(intakeFolders));
        Candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
        Filing = filing ?? throw new ArgumentNullException(nameof(filing));
        UndoActions = undoActions ?? throw new ArgumentNullException(nameof(undoActions));
    }

    public ManualMetadataCaptureViewModel Manual { get; }

    public SearchCommandViewModel Search { get; }

    public IntakeFolderSettingsViewModel IntakeFolders { get; }

    public IntakeCandidateQueueViewModel Candidates { get; }

    public SafeFileOperationViewModel Filing { get; }

    public UndoActionsViewModel UndoActions { get; }
}
