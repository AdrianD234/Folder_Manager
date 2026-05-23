namespace FileIntakeAssistant.Core.Configuration;

public sealed class IntakeFolderPathValidator
{
    private static readonly string[] IgnoredFolderSegments =
    [
        ".git",
        ".svn",
        ".hg",
        "node_modules",
        ".venv",
        "venv",
        ".tox",
        ".mypy_cache",
        ".pytest_cache",
        ".ruff_cache",
        "bin",
        "obj",
        "target",
        "dist",
        "build",
        ".vs",
        ".idea",
        ".gradle",
        ".next",
        ".nuxt",
        "coverage",
        "packages"
    ];

    public IntakeFolderPathValidationResult Validate(IntakeFolderPathValidationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return IntakeFolderPathValidationResult.Invalid("Select a folder path before adding it.");
        }

        string fullPath;
        try
        {
            fullPath = Normalize(Path.GetFullPath(request.Path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return IntakeFolderPathValidationResult.Invalid("The folder path is not a valid Windows path.");
        }

        if (!request.DirectoryExists)
        {
            return IntakeFolderPathValidationResult.Invalid("The intake folder must exist before it can be watched.");
        }

        if (IsDriveRoot(fullPath))
        {
            return IntakeFolderPathValidationResult.Invalid("Drive roots cannot be watched.");
        }

        if (IsExactProtectedRoot(fullPath, request.Options.UserProfilePath))
        {
            return IntakeFolderPathValidationResult.Invalid("The whole user profile cannot be watched.");
        }

        if (IsUnderProtectedRoot(fullPath, request.Options.AppDataPath)
            || IsUnderProtectedRoot(fullPath, request.Options.LocalAppDataPath)
            || IsUnderProtectedRoot(fullPath, request.Options.ProgramFilesPath)
            || IsUnderProtectedRoot(fullPath, request.Options.ProgramFilesX86Path)
            || IsUnderProtectedRoot(fullPath, request.Options.WindowsPath))
        {
            return IntakeFolderPathValidationResult.Invalid("System, app-data, and program folders cannot be watched.");
        }

        if (request.ContainsRepositoryMarker)
        {
            return IntakeFolderPathValidationResult.Invalid("Repository roots should use folder-level context, not intake watching.");
        }

        var segments = fullPath.Split(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (IgnoredFolderSegments.Any(ignored => segments.Any(segment => string.Equals(segment, ignored, StringComparison.OrdinalIgnoreCase))))
        {
            return IntakeFolderPathValidationResult.Invalid("Development, build, package, and cache folders cannot be watched as intake roots.");
        }

        return IntakeFolderPathValidationResult.Valid(fullPath);
    }

    private static bool IsDriveRoot(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);
        return !string.IsNullOrWhiteSpace(root)
            && string.Equals(
                Normalize(root),
                fullPath,
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExactProtectedRoot(string fullPath, string? protectedRoot)
    {
        if (string.IsNullOrWhiteSpace(protectedRoot))
        {
            return false;
        }

        return string.Equals(fullPath, Normalize(protectedRoot), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnderProtectedRoot(string fullPath, string? protectedRoot)
    {
        if (string.IsNullOrWhiteSpace(protectedRoot))
        {
            return false;
        }

        var normalizedRoot = Normalize(protectedRoot);
        return string.Equals(fullPath, normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith($"{normalizedRoot}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path)
    {
        return path
            .Trim()
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimEnd(Path.DirectorySeparatorChar);
    }
}
