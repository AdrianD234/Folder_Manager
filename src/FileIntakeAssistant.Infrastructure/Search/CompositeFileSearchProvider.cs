using FileIntakeAssistant.Core.Search;

namespace FileIntakeAssistant.Infrastructure.Search;

public sealed class CompositeFileSearchProvider : IFileSearchProvider
{
    private readonly IFileSearchProvider _primaryProvider;
    private readonly IFileSearchProvider _secondaryProvider;

    public CompositeFileSearchProvider(
        IFileSearchProvider primaryProvider,
        IFileSearchProvider secondaryProvider)
    {
        _primaryProvider = primaryProvider ?? throw new ArgumentNullException(nameof(primaryProvider));
        _secondaryProvider = secondaryProvider ?? throw new ArgumentNullException(nameof(secondaryProvider));
    }

    public string Name => $"{_primaryProvider.Name}+{_secondaryProvider.Name}";

    public async Task<SearchProviderResult> SearchAsync(
        SearchIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);

        var primaryResult = await _primaryProvider.SearchAsync(intent, cancellationToken).ConfigureAwait(false);
        var secondaryResult = await _secondaryProvider.SearchAsync(intent, cancellationToken).ConfigureAwait(false);

        var merged = new Dictionary<string, SearchResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in primaryResult.Results)
        {
            merged[NormalizePathKey(result.Path)] = result;
        }

        var canIncludeSecondaryOnly = CanIncludeSecondaryOnlyResults(intent);
        foreach (var secondary in secondaryResult.Results)
        {
            var key = NormalizePathKey(secondary.Path);
            if (merged.TryGetValue(key, out var existing))
            {
                merged[key] = MergeExistingResult(existing, secondary);
            }
            else if (canIncludeSecondaryOnly)
            {
                merged[key] = secondary;
            }
        }

        var limit = intent.Count.GetValueOrDefault(25);
        var results = merged.Values
            .OrderByDescending(result => result.Score)
            .ThenByDescending(result => result.MatchedAt)
            .ThenBy(result => result.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();

        return new SearchProviderResult(Name, intent, results);
    }

    private static SearchResult MergeExistingResult(SearchResult existing, SearchResult secondary)
    {
        var reasons = existing.MatchedReasons
            .Concat(secondary.MatchedReasons)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return existing with
        {
            MatchedReasons = reasons,
            Score = existing.Score + Math.Min(secondary.Score, 5)
        };
    }

    private static bool CanIncludeSecondaryOnlyResults(SearchIntent intent)
    {
        return intent.Relevance is null
            && intent.Project is null
            && intent.Topic is null
            && intent.DateRange is null
            && intent.SourceHint is null;
    }

    private static string NormalizePathKey(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
