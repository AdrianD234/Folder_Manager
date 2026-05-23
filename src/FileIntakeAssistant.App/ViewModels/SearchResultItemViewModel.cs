using FileIntakeAssistant.Core.Search;

namespace FileIntakeAssistant.App.ViewModels;

public sealed class SearchResultItemViewModel
{
    public SearchResultItemViewModel(SearchResult result)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public SearchResult Result { get; }

    public SearchResultTarget Target => Result.Target;

    public long RecordId => Result.RecordId;

    public string DisplayName => Result.DisplayName;

    public string Path => Result.Path;

    public string? ContainingFolder => Result.ContainingFolder;

    public string? Extension => Result.Extension;

    public string? Relevance => Result.Relevance;

    public string? Project => Result.Project;

    public string? Topic => Result.Topic;

    public double Score => Result.Score;

    public string MatchedReasons => string.Join(", ", Result.MatchedReasons);
}
