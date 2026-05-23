using System.Diagnostics;
using System.IO;

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
    private readonly Func<ProcessStartInfo, Process?> _startProcess;

    public WindowsFileLaunchService()
        : this(Process.Start)
    {
    }

    public WindowsFileLaunchService(Func<ProcessStartInfo, Process?> startProcess)
    {
        _startProcess = startProcess ?? throw new ArgumentNullException(nameof(startProcess));
    }

    public Task<FileLaunchResult> OpenFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        return LaunchAsync(path, LaunchTargetKind.File);
    }

    public Task<FileLaunchResult> OpenFolderAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        return LaunchAsync(path, LaunchTargetKind.Folder);
    }

    private Task<FileLaunchResult> LaunchAsync(string path, LaunchTargetKind targetKind)
    {
        var validation = ValidateLaunchPath(path, targetKind);
        if (!validation.Succeeded)
        {
            return Task.FromResult(new FileLaunchResult(false, validation.FailureReason));
        }

        try
        {
            _startProcess(new ProcessStartInfo
            {
                FileName = validation.FullPath!,
                UseShellExecute = true
            });

            return Task.FromResult(new FileLaunchResult(true, null));
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return Task.FromResult(new FileLaunchResult(false, ex.Message));
        }
    }

    private static LaunchPathValidation ValidateLaunchPath(string path, LaunchTargetKind targetKind)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return LaunchPathValidation.Failed("Path is required.");
        }

        var trimmedPath = path.Trim();
        if (LooksLikeProtocolTarget(trimmedPath))
        {
            return LaunchPathValidation.Failed("Only local filesystem paths can be opened.");
        }

        if (!Path.IsPathFullyQualified(trimmedPath))
        {
            return LaunchPathValidation.Failed("Only fully qualified local filesystem paths can be opened.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(trimmedPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return LaunchPathValidation.Failed("The path is not a valid local filesystem path.");
        }

        return targetKind switch
        {
            LaunchTargetKind.File when !File.Exists(fullPath) =>
                LaunchPathValidation.Failed("The file does not exist."),
            LaunchTargetKind.Folder when !Directory.Exists(fullPath) =>
                LaunchPathValidation.Failed("The folder does not exist."),
            _ => LaunchPathValidation.Success(fullPath)
        };
    }

    private static bool LooksLikeProtocolTarget(string path)
    {
        if (path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("http:", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var colonIndex = path.IndexOf(':');
        if (colonIndex < 0)
        {
            return false;
        }

        if (colonIndex != 1 || !char.IsLetter(path[0]))
        {
            return true;
        }

        return path.IndexOf(':', colonIndex + 1) >= 0;
    }

    private enum LaunchTargetKind
    {
        File,
        Folder
    }

    private sealed record LaunchPathValidation(bool Succeeded, string? FailureReason, string? FullPath)
    {
        public static LaunchPathValidation Success(string fullPath)
        {
            return new LaunchPathValidation(true, null, fullPath);
        }

        public static LaunchPathValidation Failed(string failureReason)
        {
            return new LaunchPathValidation(false, failureReason, null);
        }
    }
}
