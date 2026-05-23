namespace FileIntakeAssistant.Core.FileOperations;

public sealed class SafeFileOperationPlanner
{
    private const int MaxFileNameLength = 255;
    private const int MaxPathLength = 32_767;

    private readonly FileNameSanitizer _fileNameSanitizer;
    private readonly FileNameConflictResolver _conflictResolver;

    public SafeFileOperationPlanner()
        : this(new FileNameSanitizer(), new FileNameConflictResolver())
    {
    }

    public SafeFileOperationPlanner(
        FileNameSanitizer fileNameSanitizer,
        FileNameConflictResolver conflictResolver)
    {
        _fileNameSanitizer = fileNameSanitizer;
        _conflictResolver = conflictResolver;
    }

    public SafeFileOperationPlan Plan(SafeFileOperationPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();
        var sourcePath = NormalizePath(request.SourcePath, errors, "Source path");
        var destinationDirectory = NormalizePath(request.DestinationDirectory, errors, "Destination directory");
        var originalExtension = Path.GetExtension(sourcePath);
        var sanitizedFileName = _fileNameSanitizer.Sanitize(
            request.RequestedFileName,
            originalExtension,
            request.AllowExtensionChange);

        ValidateFileName(sanitizedFileName, errors);

        var destinationPath = errors.Count == 0
            ? _conflictResolver.ResolveNonConflictingPath(
                destinationDirectory,
                sanitizedFileName,
                request.ExistingDestinationPaths)
            : Path.Combine(destinationDirectory, sanitizedFileName);

        var extensionPreserved = string.Equals(
            Path.GetExtension(sourcePath),
            Path.GetExtension(destinationPath),
            StringComparison.OrdinalIgnoreCase);

        if (!request.AllowExtensionChange && !extensionPreserved)
        {
            errors.Add("The destination extension must match the source extension unless extension changes are explicitly allowed.");
        }

        if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("The destination path must differ from the source path.");
        }

        if (destinationPath.Length > MaxPathLength)
        {
            errors.Add("The destination path is too long.");
        }

        return new SafeFileOperationPlan(
            PlanId: Guid.NewGuid(),
            OperationKind: request.OperationKind,
            SourcePath: sourcePath,
            DestinationDirectory: destinationDirectory,
            RequestedFileName: request.RequestedFileName,
            SanitizedFileName: sanitizedFileName,
            DestinationPath: destinationPath,
            RequiresConfirmation: true,
            WouldCreateDestinationDirectory: !request.DestinationDirectoryExists,
            ExtensionPreserved: extensionPreserved,
            IsValid: errors.Count == 0,
            ValidationErrors: errors);
    }

    private static string NormalizePath(string path, List<string> errors, string label)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            errors.Add($"{label} is required.");
            return string.Empty;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!Path.IsPathRooted(fullPath))
            {
                errors.Add($"{label} must be rooted.");
            }

            return fullPath;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            errors.Add($"{label} is invalid.");
            return path;
        }
    }

    private static void ValidateFileName(string sanitizedFileName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(sanitizedFileName))
        {
            errors.Add("A destination filename is required.");
            return;
        }

        if (sanitizedFileName.Length > MaxFileNameLength)
        {
            errors.Add("The destination filename is too long.");
        }

        if (!string.Equals(sanitizedFileName, Path.GetFileName(sanitizedFileName), StringComparison.Ordinal))
        {
            errors.Add("The destination filename must not contain path separators.");
        }
    }
}
