using FileIntakeAssistant.Core.Search;

namespace FileIntakeAssistant.Tests.Search;

public sealed class SearchIntentParserTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 23, 12, 0, 0, TimeSpan.FromHours(12));

    private readonly SearchIntentParser _parser = new();

    [Fact]
    public void Search_ParsesOpenLastFiveExcelFiles()
    {
        var intent = _parser.Parse("open the last five Excel files I saved", FixedNow);

        Assert.Equal(SearchIntentAction.OpenFiles, intent.Action);
        Assert.Equal(SearchResultTarget.File, intent.Target);
        Assert.Equal(5, intent.Count);
        Assert.Equal("Excel", intent.FileType);
        Assert.Equal([".xls", ".xlsx"], intent.Extensions);
        Assert.Equal(SearchSortMode.MostRecent, intent.SortMode);
        Assert.Empty(intent.Keywords);
        Assert.Contains("\"action\":\"OpenFiles\"", intent.ToStableJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void Search_ParsesHighRelevancePdfsFromLastWeek()
    {
        var intent = _parser.Parse("show high relevance PDFs from last week", FixedNow);

        Assert.Equal(SearchIntentAction.ShowResults, intent.Action);
        Assert.Equal("PDF", intent.FileType);
        Assert.Equal([".pdf"], intent.Extensions);
        Assert.Equal("high", intent.Relevance);
        Assert.NotNull(intent.DateRange);
        Assert.Equal("last week", intent.DateRange.Label);
        Assert.Equal(new DateTimeOffset(2026, 5, 11, 0, 0, 0, TimeSpan.FromHours(12)), intent.DateRange.FromInclusive);
        Assert.Equal(new DateTimeOffset(2026, 5, 18, 0, 0, 0, TimeSpan.FromHours(12)), intent.DateRange.ToExclusive);
        Assert.Empty(intent.Keywords);
    }

    [Fact]
    public void Search_ParsesFindFilesAboutNvidiaCapex()
    {
        var intent = _parser.Parse("find files about Nvidia capex", FixedNow);

        Assert.Equal(SearchIntentAction.ShowResults, intent.Action);
        Assert.Equal(SearchResultTarget.File, intent.Target);
        Assert.Equal(["capex", "nvidia"], intent.Keywords);
        Assert.Null(intent.FileType);
        Assert.Null(intent.DateRange);
    }

    [Fact]
    public void Search_ParsesOpenFolderForAiInfrastructureReports()
    {
        var intent = _parser.Parse("open the folder for my AI infrastructure reports", FixedNow);

        Assert.Equal(SearchIntentAction.OpenContainingFolder, intent.Action);
        Assert.Equal(SearchResultTarget.Folder, intent.Target);
        Assert.Equal(["ai", "infrastructure", "reports"], intent.Keywords);
    }

    [Theory]
    [InlineData("today", "today", "2026-05-23T00:00:00+12:00", "2026-05-24T00:00:00+12:00")]
    [InlineData("yesterday", "yesterday", "2026-05-22T00:00:00+12:00", "2026-05-23T00:00:00+12:00")]
    [InlineData("this week", "this week", "2026-05-18T00:00:00+12:00", "2026-05-25T00:00:00+12:00")]
    [InlineData("last week", "last week", "2026-05-11T00:00:00+12:00", "2026-05-18T00:00:00+12:00")]
    [InlineData("last three weeks", "last 3 weeks", "2026-04-27T00:00:00+12:00", "2026-05-18T00:00:00+12:00")]
    [InlineData("last month", "last month", "2026-04-01T00:00:00+12:00", "2026-05-01T00:00:00+12:00")]
    [InlineData("last year", "last year", "2025-01-01T00:00:00+12:00", "2026-01-01T00:00:00+12:00")]
    public void Search_ParsesRelativeDates(
        string datePhrase,
        string expectedLabel,
        string expectedFrom,
        string expectedTo)
    {
        var intent = _parser.Parse($"show files from {datePhrase}", FixedNow);

        Assert.NotNull(intent.DateRange);
        Assert.Equal(expectedLabel, intent.DateRange.Label);
        Assert.Equal(DateTimeOffset.Parse(expectedFrom), intent.DateRange.FromInclusive);
        Assert.Equal(DateTimeOffset.Parse(expectedTo), intent.DateRange.ToExclusive);
    }

    [Fact]
    public void VoiceCommand_DestructiveCommandIsUnsupported()
    {
        var intent = _parser.Parse("delete the old PDFs", FixedNow);

        Assert.Equal(SearchIntentAction.Unsupported, intent.Action);
        Assert.Contains("Destructive", intent.UnsupportedReason, StringComparison.OrdinalIgnoreCase);
    }
}
