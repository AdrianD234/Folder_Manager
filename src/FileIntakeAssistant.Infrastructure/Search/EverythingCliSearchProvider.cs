using System.Globalization;
using FileIntakeAssistant.Core.Search;

namespace FileIntakeAssistant.Infrastructure.Search;

public sealed record EverythingCliSearchProviderOptions
{
    public bool Enabled { get; init; }

    public string? ExecutablePath { get; init; }

    public bool DiscoverOnPath { get; init; } = true;

    public IReadOnlyList<string> AllowedRoots { get; init; } = [];

    public int MaxResults { get; init; } = 50;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}

public sealed class EverythingCliSearchProvider : IFileSearchProvider
{
    private readonly EverythingCliSearchProviderOptions _options;
    private readonly IEverythingCliProcessRunner _processRunner;
    private readonly IEverythingCliPathResolver _pathResolver;

    public EverythingCliSearchProvider(
        EverythingCliSearchProviderOptions? options = null,
        IEverythingCliProcessRunner? processRunner = null,
        IEverythingCliPathResolver? pathResolver = null)
    {
        _options = options ?? new EverythingCliSearchProviderOptions();
        _processRunner = processRunner ?? new SystemEverythingCliProcessRunner();
        _pathResolver = pathResolver ?? new EverythingCliPathResolver();
    }

    public string Name => "EverythingCLI";

    public async Task<SearchProviderResult> SearchAsync(
        SearchIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (!_options.Enabled)
        {
            return Empty(intent);
        }

        var allowedRoots = NormalizeAllowedRoots(_options.AllowedRoots);
        if (allowedRoots.Count == 0)
        {
            return Empty(intent);
        }

        var executablePath = _pathResolver.Resolve(_options);
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return Empty(intent);
        }

        var arguments = BuildArguments(intent);
        var processResult = await _processRunner.RunAsync(
            executablePath,
            arguments,
            _options.Timeout,
            cancellationToken).ConfigureAwait(false);

        if (processResult.ExitCode != 0)
        {
            return Empty(intent);
        }

        var results = ParseOutput(processResult.StandardOutput, intent, allowedRoots)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(intent.Count ?? _options.MaxResults)
            .ToArray();

        return new SearchProviderResult(Name, intent, results);
    }

    private static IReadOnlyList<string> NormalizeAllowedRoots(IReadOnlyList<string> roots)
    {
        return roots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<string> BuildArguments(SearchIntent intent)
    {
        var query = BuildQuery(intent);
        var limit = Math.Max(1, intent.Count.GetValueOrDefault(_options.MaxResults));
        return ["-n", limit.ToString(CultureInfo.InvariantCulture), "-full-path-and-name", query];
    }

    private static string BuildQuery(SearchIntent intent)
    {
        var terms = new List<string>();

        if (intent.Extensions.Count > 0)
        {
            terms.Add(string.Join(
                " | ",
                intent.Extensions.Select(extension => $"ext:{extension.TrimStart('.')}")));
        }

        terms.AddRange(intent.Keywords.Select(QuoteIfNeeded));

        if (terms.Count == 0 && !string.IsNullOrWhiteSpace(intent.FileType))
        {
            terms.Add(QuoteIfNeeded(intent.FileType));
        }

        return terms.Count == 0 ? "*" : string.Join(' ', terms);
    }

    private static IReadOnlyList<SearchResult> ParseOutput(
        string output,
        SearchIntent intent,
        IReadOnlyList<string> allowedRoots)
    {
        var results = new List<SearchResult>();
        foreach (var rawLine in output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            var path = rawLine.Trim();
            if (path.Length == 0 || !Path.IsPathRooted(path))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(path);
            if (allowedRoots.Count > 0 && !IsUnderAllowedRoot(fullPath, allowedRoots))
            {
                continue;
            }

            if (intent.Extensions.Count > 0
                && !intent.Extensions.Contains(Path.GetExtension(fullPath), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var searchable = fullPath;
            var matchedKeywords = intent.Keywords
                .Where(keyword => ContainsNormalized(searchable, keyword))
                .ToArray();
            if (intent.Keywords.Count > 0 && matchedKeywords.Length != intent.Keywords.Count)
            {
                continue;
            }

            var target = intent.Target == SearchResultTarget.Folder
                ? SearchResultTarget.Folder
                : SearchResultTarget.File;
            var displayName = target == SearchResultTarget.Folder
                ? Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                : Path.GetFileName(fullPath);

            var reasons = new List<string> { "Everything CLI path match" };
            reasons.AddRange(matchedKeywords.Select(keyword => $"keyword {keyword}"));

            var score = 10d + matchedKeywords.Length * 5;
            if (intent.Extensions.Count > 0)
            {
                score += 10;
                reasons.Add($"file type {intent.FileType}");
            }

            results.Add(new SearchResult(
                Target: target,
                RecordId: 0,
                DisplayName: string.IsNullOrWhiteSpace(displayName) ? fullPath : displayName,
                Path: fullPath,
                ContainingFolder: target == SearchResultTarget.Folder ? fullPath : Path.GetDirectoryName(fullPath),
                Extension: target == SearchResultTarget.Folder ? null : Path.GetExtension(fullPath),
                MatchedAt: null,
                Relevance: null,
                Project: null,
                Topic: null,
                MatchedReasons: reasons,
                Score: score));
        }

        return results;
    }

    private static bool IsUnderAllowedRoot(string path, IReadOnlyList<string> allowedRoots)
    {
        return allowedRoots.Any(root =>
            path.Equals(root, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsNormalized(string haystack, string needle)
    {
        if (string.IsNullOrWhiteSpace(haystack) || string.IsNullOrWhiteSpace(needle))
        {
            return false;
        }

        var normalizedHaystack = NormalizeForSearch(haystack);
        var normalizedNeedle = NormalizeForSearch(needle);
        var singularNeedle = normalizedNeedle.EndsWith('s') && normalizedNeedle.Length > 3
            ? normalizedNeedle[..^1]
            : normalizedNeedle;

        return normalizedHaystack.Contains(normalizedNeedle, StringComparison.OrdinalIgnoreCase)
            || normalizedHaystack.Contains(singularNeedle, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeForSearch(string value)
    {
        var chars = value.ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
            .ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string QuoteIfNeeded(string term)
    {
        return term.Contains(' ', StringComparison.Ordinal)
            ? $"\"{term}\""
            : term;
    }

    private SearchProviderResult Empty(SearchIntent intent)
    {
        return new SearchProviderResult(Name, intent, []);
    }
}
