namespace FileIntakeAssistant.Infrastructure.Search;

public interface IEverythingCliPathResolver
{
    string? Resolve(EverythingCliSearchProviderOptions options);
}

public sealed class EverythingCliPathResolver : IEverythingCliPathResolver
{
    public string? Resolve(EverythingCliSearchProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.ExecutablePath)
            && File.Exists(options.ExecutablePath))
        {
            return Path.GetFullPath(options.ExecutablePath);
        }

        if (!options.DiscoverOnPath)
        {
            return null;
        }

        return FindOnPath("es.exe");
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim(), fileName);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }
}
