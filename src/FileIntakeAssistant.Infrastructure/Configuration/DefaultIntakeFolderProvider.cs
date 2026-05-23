using FileIntakeAssistant.Core.Models;

namespace FileIntakeAssistant.Infrastructure.Configuration;

public static class DefaultIntakeFolderProvider
{
    public static IntakeFolder CreateDownloadsSuggestion(DateTimeOffset now)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloadsPath = string.IsNullOrWhiteSpace(userProfile)
            ? "Downloads"
            : Path.Combine(userProfile, "Downloads");

        return new IntakeFolder(
            Id: null,
            Path: downloadsPath,
            DisplayName: "Downloads",
            Enabled: false,
            FolderType: "Downloads",
            Recursive: false,
            CreatedAt: now,
            UpdatedAt: now);
    }
}
