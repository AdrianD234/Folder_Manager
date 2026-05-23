namespace FileIntakeAssistant.Core.Search;

public interface IFileSearchProvider
{
    string Name { get; }

    Task<SearchProviderResult> SearchAsync(
        SearchIntent intent,
        CancellationToken cancellationToken = default);
}
