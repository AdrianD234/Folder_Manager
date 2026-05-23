using FileIntakeAssistant.Core.Models;

namespace FileIntakeAssistant.App.ViewModels;

public sealed class IntakeFolderItemViewModel
{
    public IntakeFolderItemViewModel(IntakeFolder folder)
    {
        Folder = folder ?? throw new ArgumentNullException(nameof(folder));
    }

    public IntakeFolder Folder { get; }

    public long? Id => Folder.Id;

    public string Path => Folder.Path;

    public string DisplayName => Folder.DisplayName;

    public bool Enabled => Folder.Enabled;

    public bool Recursive => Folder.Recursive;

    public string FolderType => Folder.FolderType;

    public string Status => Enabled ? "Enabled" : "Disabled";
}
