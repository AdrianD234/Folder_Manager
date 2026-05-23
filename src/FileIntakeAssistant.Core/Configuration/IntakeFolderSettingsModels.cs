using FileIntakeAssistant.Core.Models;

namespace FileIntakeAssistant.Core.Configuration;

public sealed record IntakeFolderPathValidationOptions(
    string? UserProfilePath,
    string? AppDataPath,
    string? LocalAppDataPath,
    string? ProgramFilesPath,
    string? ProgramFilesX86Path,
    string? WindowsPath)
{
    public static IntakeFolderPathValidationOptions FromCurrentEnvironment()
    {
        return new IntakeFolderPathValidationOptions(
            UserProfilePath: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            AppDataPath: Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            LocalAppDataPath: Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProgramFilesPath: Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            ProgramFilesX86Path: Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            WindowsPath: Environment.GetFolderPath(Environment.SpecialFolder.Windows));
    }
}

public sealed record IntakeFolderPathValidationRequest(
    string Path,
    IntakeFolderPathValidationOptions Options,
    bool DirectoryExists,
    bool ContainsRepositoryMarker);

public sealed record IntakeFolderPathValidationResult(
    bool IsValid,
    string? NormalizedPath,
    string Reason)
{
    public static IntakeFolderPathValidationResult Valid(string normalizedPath)
    {
        return new IntakeFolderPathValidationResult(true, normalizedPath, "Path is acceptable as an explicit intake folder.");
    }

    public static IntakeFolderPathValidationResult Invalid(string reason)
    {
        return new IntakeFolderPathValidationResult(false, null, reason);
    }
}

public sealed record IntakeFolderSettingsRequest(
    string Path,
    string? DisplayName,
    string FolderType,
    bool Enabled,
    bool Recursive,
    bool DirectoryExists,
    bool ContainsRepositoryMarker);

public sealed record IntakeFolderSettingsResult(
    bool Succeeded,
    string Message,
    IntakeFolder? Folder)
{
    public static IntakeFolderSettingsResult Success(string message, IntakeFolder folder)
    {
        return new IntakeFolderSettingsResult(true, message, folder);
    }

    public static IntakeFolderSettingsResult Failure(string message)
    {
        return new IntakeFolderSettingsResult(false, message, null);
    }
}
