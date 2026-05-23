using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileIntakeAssistant.Core.Search;

public enum SearchIntentAction
{
    ShowResults,
    OpenFiles,
    OpenContainingFolder,
    Unsupported
}

public enum SearchResultTarget
{
    File,
    Folder
}

public enum SearchSortMode
{
    BestMatch,
    MostRecent
}

public enum SearchExecutionOutcome
{
    ShowResults,
    ShowSingleConfirmation,
    ShowBulkConfirmation,
    Unsupported
}

public sealed record SearchDateRange(
    string Label,
    DateTimeOffset FromInclusive,
    DateTimeOffset ToExclusive);

public sealed record SearchIntent(
    string RawText,
    SearchIntentAction Action,
    SearchResultTarget Target,
    int? Count,
    string? FileType,
    IReadOnlyList<string> Extensions,
    string? Relevance,
    string? Project,
    string? Topic,
    IReadOnlyList<string> Keywords,
    SearchDateRange? DateRange,
    string? SourceHint,
    SearchSortMode SortMode,
    bool IsAmbiguous,
    string? UnsupportedReason)
{
    public bool IsSupported => Action != SearchIntentAction.Unsupported;

    public string ToStableJson()
    {
        return JsonSerializer.Serialize(this, SearchIntentJson.Options);
    }
}

public sealed record SearchResult(
    SearchResultTarget Target,
    long RecordId,
    string DisplayName,
    string Path,
    string? ContainingFolder,
    string? Extension,
    DateTimeOffset? MatchedAt,
    string? Relevance,
    string? Project,
    string? Topic,
    IReadOnlyList<string> MatchedReasons,
    double Score);

public sealed record SearchProviderResult(
    string Provider,
    SearchIntent Intent,
    IReadOnlyList<SearchResult> Results);

public sealed record SearchWorkflowResult(
    SearchIntent Intent,
    IReadOnlyList<SearchResult> Results,
    SearchExecutionOutcome Outcome,
    long? VoiceCommandId,
    long? SearchQueryId,
    bool RequiresConfirmation);

public static class SearchIntentJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
