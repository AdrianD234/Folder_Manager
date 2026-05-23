using System.Diagnostics;

namespace FileIntakeAssistant.App.ViewModels;

public interface IFileLaunchService
{
    Task<FileLaunchResult> OpenFileAsync(
        string path,
        CancellationToken cancellationToken = default);

    Task<FileLaunchResult> OpenFolderAsync(
        string path,
        CancellationToken cancellationToken = default);
}

public sealed record FileLaunchResult(bool Succeeded, string? FailureReason);

public sealed class WindowsFileLaunchService : IFileLaunchService
{
    public Task<FileLaunchResult> OpenFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        return LaunchAsync(path);
    }

    public Task<FileLaunchResult> OpenFolderAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        return LaunchAsync(path);
    }

    private static Task<FileLaunchResult> LaunchAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(new FileLaunchResult(false, "Path is required."));
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });

            return Task.FromResult(new FileLaunchResult(true, null));
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return Task.FromResult(new FileLaunchResult(false, ex.Message));
        }
    }
}
