namespace FileIntakeAssistant.Core.Search;

public static class SearchConfirmationPolicy
{
    public static SearchExecutionOutcome DetermineOutcome(SearchIntent intent, int resultCount)
    {
        if (!intent.IsSupported)
        {
            return SearchExecutionOutcome.Unsupported;
        }

        if (intent.Action is SearchIntentAction.OpenFiles or SearchIntentAction.OpenContainingFolder)
        {
            return resultCount == 1
                ? SearchExecutionOutcome.ShowSingleConfirmation
                : SearchExecutionOutcome.ShowBulkConfirmation;
        }

        return SearchExecutionOutcome.ShowResults;
    }

    public static bool RequiresConfirmation(SearchExecutionOutcome outcome)
    {
        return outcome is SearchExecutionOutcome.ShowSingleConfirmation
            or SearchExecutionOutcome.ShowBulkConfirmation;
    }
}
