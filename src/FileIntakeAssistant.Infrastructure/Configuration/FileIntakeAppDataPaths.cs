namespace FileIntakeAssistant.Infrastructure.Configuration;

public sealed record FileIntakeAppDataPaths(
    string Root,
    string DataDirectory,
    string DatabasePath,
    string LogsDirectory,
    string TempAudioDirectory,
    string ConfigDirectory);

public static class FileIntakeAppDataPathProvider
{
    public static FileIntakeAppDataPaths GetDefault()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        var root = Path.Combine(localAppData, "File Intake Assistant");
        var dataDirectory = Path.Combine(root, "data");
        return new FileIntakeAppDataPaths(
            Root: root,
            DataDirectory: dataDirectory,
            DatabasePath: Path.Combine(dataDirectory, "file-intake.db"),
            LogsDirectory: Path.Combine(root, "logs"),
            TempAudioDirectory: Path.Combine(root, "temp-audio"),
            ConfigDirectory: Path.Combine(root, "config"));
    }
}
