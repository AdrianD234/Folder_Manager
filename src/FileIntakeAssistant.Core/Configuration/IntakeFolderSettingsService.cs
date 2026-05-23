using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;

namespace FileIntakeAssistant.Core.Configuration;

public sealed class IntakeFolderSettingsService
{
    private readonly IFileIntakeStore _store;
    private readonly IntakeFolderPathValidator _validator;
    private readonly IntakeFolderPathValidationOptions _validationOptions;

    public IntakeFolderSettingsService(
        IFileIntakeStore store,
        IntakeFolderPathValidator validator,
        IntakeFolderPathValidationOptions validationOptions)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _validationOptions = validationOptions ?? throw new ArgumentNullException(nameof(validationOptions));
    }

    public async Task<IReadOnlyList<IntakeFolder>> ListWithSuggestionAsync(
        IntakeFolder? defaultSuggestion,
        CancellationToken cancellationToken = default)
    {
        var folders = await _store.ListIntakeFoldersAsync(enabledOnly: false, cancellationToken)
            .ConfigureAwait(false);

        if (defaultSuggestion is null
            || folders.Any(folder => string.Equals(
                Normalize(folder.Path),
                Normalize(defaultSuggestion.Path),
                StringComparison.OrdinalIgnoreCase)))
        {
            return folders;
        }

        return folders
            .Append(defaultSuggestion)
            .OrderBy(folder => folder.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IntakeFolderSettingsResult> AddOrUpdateAsync(
        IntakeFolderSettingsRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var validation = _validator.Validate(new IntakeFolderPathValidationRequest(
            request.Path,
            _validationOptions,
            request.DirectoryExists,
            request.ContainsRepositoryMarker));

        if (!validation.IsValid || validation.NormalizedPath is null)
        {
            return IntakeFolderSettingsResult.Failure(validation.Reason);
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? GetDisplayName(validation.NormalizedPath)
            : request.DisplayName.Trim();
        var folderType = string.IsNullOrWhiteSpace(request.FolderType)
            ? "Intake"
            : request.FolderType.Trim();

        var existing = await _store.GetIntakeFolderByPathAsync(validation.NormalizedPath, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            var updated = existing with
            {
                DisplayName = displayName,
                Enabled = request.Enabled,
                FolderType = folderType,
                Recursive = request.Recursive,
                UpdatedAt = now
            };

            await _store.UpdateIntakeFolderAsync(updated, cancellationToken).ConfigureAwait(false);
            return IntakeFolderSettingsResult.Success("Updated intake folder.", updated);
        }

        var folder = new IntakeFolder(
            Id: null,
            Path: validation.NormalizedPath,
            DisplayName: displayName,
            Enabled: request.Enabled,
            FolderType: folderType,
            Recursive: request.Recursive,
            CreatedAt: now,
            UpdatedAt: now);

        var id = await _store.AddIntakeFolderAsync(folder, cancellationToken).ConfigureAwait(false);
        return IntakeFolderSettingsResult.Success("Added intake folder.", folder with { Id = id });
    }

    public Task<IntakeFolderSettingsResult> EnableAsync(
        IntakeFolder folder,
        DateTimeOffset now,
        bool directoryExists,
        bool containsRepositoryMarker,
        CancellationToken cancellationToken = default)
    {
        return folder.Id is null
            ? AddOrUpdateAsync(
                new IntakeFolderSettingsRequest(
                    folder.Path,
                    folder.DisplayName,
                    folder.FolderType,
                    Enabled: true,
                    folder.Recursive,
                    directoryExists,
                    containsRepositoryMarker),
                now,
                cancellationToken)
            : SetEnabledAsync(folder.Id.Value, enabled: true, now, cancellationToken);
    }

    public Task<IntakeFolderSettingsResult> DisableAsync(
        long id,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        return SetEnabledAsync(id, enabled: false, now, cancellationToken);
    }

    public Task<IntakeFolderSettingsResult> RemoveFromWatchListAsync(
        long id,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        return SetEnabledAsync(id, enabled: false, now, cancellationToken, "Removed intake folder from active watch list.");
    }

    private async Task<IntakeFolderSettingsResult> SetEnabledAsync(
        long id,
        bool enabled,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        string? successMessage = null)
    {
        var existing = await _store.GetIntakeFolderAsync(id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return IntakeFolderSettingsResult.Failure("The intake folder record no longer exists.");
        }

        var updated = existing with
        {
            Enabled = enabled,
            UpdatedAt = now
        };

        await _store.UpdateIntakeFolderAsync(updated, cancellationToken).ConfigureAwait(false);
        return IntakeFolderSettingsResult.Success(
            successMessage ?? (enabled ? "Enabled intake folder." : "Disabled intake folder."),
            updated);
    }

    private static string Normalize(string path)
    {
        return Path.GetFullPath(path)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimEnd(Path.DirectorySeparatorChar);
    }

    private static string GetDisplayName(string path)
    {
        return Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) switch
        {
            null or "" => path,
            var name => name
        };
    }
}
