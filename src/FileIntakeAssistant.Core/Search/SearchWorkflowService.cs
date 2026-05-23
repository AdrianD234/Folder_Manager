using FileIntakeAssistant.Core.Models;
using FileIntakeAssistant.Core.Persistence;

namespace FileIntakeAssistant.Core.Search;

public sealed class SearchWorkflowService
{
    private readonly SearchIntentParser _parser;
    private readonly IFileSearchProvider _searchProvider;
    private readonly IFileIntakeStore _store;

    public SearchWorkflowService(
        SearchIntentParser parser,
        IFileSearchProvider searchProvider,
        IFileIntakeStore store)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _searchProvider = searchProvider ?? throw new ArgumentNullException(nameof(searchProvider));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<SearchWorkflowResult> ExecuteAsync(
        string rawCommand,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default)
    {
        var intent = _parser.Parse(rawCommand, requestedAt);
        var intentJson = intent.ToStableJson();

        if (!intent.IsSupported)
        {
            var unsupportedVoiceCommandId = await _store.AddVoiceCommandAsync(new VoiceCommandRecord(
                Id: null,
                RawText: rawCommand,
                ParsedIntentJson: intentJson,
                Status: "Unsupported",
                ResultCount: 0,
                ExecutedAction: "ShowUnsupportedMessage",
                CreatedAt: requestedAt,
                DetailsJson: intent.UnsupportedReason is null
                    ? null
                    : $$"""{"reason":{{JsonString(intent.UnsupportedReason)}}}"""),
                cancellationToken).ConfigureAwait(false);

            return new SearchWorkflowResult(
                Intent: intent,
                Results: [],
                Outcome: SearchExecutionOutcome.Unsupported,
                VoiceCommandId: unsupportedVoiceCommandId,
                SearchQueryId: null,
                RequiresConfirmation: false);
        }

        var providerResult = await _searchProvider.SearchAsync(intent, cancellationToken).ConfigureAwait(false);
        var results = providerResult.Results;
        var outcome = SearchConfirmationPolicy.DetermineOutcome(intent, results.Count);
        var requiresConfirmation = SearchConfirmationPolicy.RequiresConfirmation(outcome);
        var executedAction = outcome switch
        {
            SearchExecutionOutcome.ShowSingleConfirmation => "ShowSingleConfirmation",
            SearchExecutionOutcome.ShowBulkConfirmation => "ShowBulkConfirmation",
            _ => "ShowResults"
        };

        var voiceCommandId = await _store.AddVoiceCommandAsync(new VoiceCommandRecord(
            Id: null,
            RawText: rawCommand,
            ParsedIntentJson: intentJson,
            Status: results.Count == 0 ? "NoResults" : "ResultsReady",
            ResultCount: results.Count,
            ExecutedAction: executedAction,
            CreatedAt: requestedAt,
            DetailsJson: $$"""{"requiresConfirmation":{{requiresConfirmation.ToString().ToLowerInvariant()}}}"""),
            cancellationToken).ConfigureAwait(false);

        var searchQueryId = await _store.AddSearchQueryAsync(new SearchQueryRecord(
            Id: null,
            QueryText: rawCommand,
            ParsedIntentJson: intentJson,
            Provider: providerResult.Provider,
            ResultCount: results.Count,
            CreatedAt: requestedAt),
            cancellationToken).ConfigureAwait(false);

        return new SearchWorkflowResult(
            Intent: intent,
            Results: results,
            Outcome: outcome,
            VoiceCommandId: voiceCommandId,
            SearchQueryId: searchQueryId,
            RequiresConfirmation: requiresConfirmation);
    }

    private static string JsonString(string value)
    {
        return System.Text.Json.JsonSerializer.Serialize(value);
    }
}
