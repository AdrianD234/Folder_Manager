using FileIntakeAssistant.Core.Batching;

namespace FileIntakeAssistant.Tests.Batch;

public sealed class BatchDetectorTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 5, 23, 4, 20, 0, TimeSpan.Zero);

    private readonly BatchDetector _detector = new();

    [Fact]
    public void Batch_DefaultThresholdsMatchDocumentedRules()
    {
        var options = BatchDetectionOptions.Defaults;

        Assert.Equal(10, options.PossibleBatchFileCountThreshold);
        Assert.Equal(TimeSpan.FromSeconds(10), options.PossibleBatchWindow);
        Assert.Equal(50, options.SuppressIndividualPromptsFileCountThreshold);
        Assert.Equal(TimeSpan.FromSeconds(60), options.SuppressIndividualPromptsWindow);
        Assert.Equal(200, options.BatchReviewOnlyFileCountThreshold);
        Assert.Equal(TimeSpan.FromMinutes(5), options.BatchReviewOnlyWindow);
    }

    [Fact]
    public void Batch_TenFilesInTenSecondsIsNotABatchBecauseRuleSaysMoreThanTen()
    {
        var result = _detector.Evaluate(Request(@"C:\Intake", count: 10, spacing: TimeSpan.FromSeconds(1)));

        Assert.Equal(BatchPromptDecision.NoBatch, result.Decision);
        Assert.False(result.SuppressIndividualPrompts);
        Assert.Equal(10, result.FileCount);
    }

    [Fact]
    public void Batch_MoreThanTenFilesInTenSecondsCreatesPossibleBatch()
    {
        var result = _detector.Evaluate(Request(@"C:\Intake", count: 11, spacing: TimeSpan.FromMilliseconds(500)));

        Assert.Equal(BatchPromptDecision.PossibleBatch, result.Decision);
        Assert.Equal(EventBatchType.UnknownBurst, result.BatchType);
        Assert.False(result.SuppressIndividualPrompts);
        Assert.Equal(11, result.FileCount);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Fact]
    public void Batch_MoreThanFiftyFilesInSixtySecondsSuppressesIndividualPrompts()
    {
        var result = _detector.Evaluate(Request(@"C:\Intake", count: 51, spacing: TimeSpan.FromSeconds(1)));

        Assert.Equal(BatchPromptDecision.SuppressIndividualPrompts, result.Decision);
        Assert.Equal(EventBatchType.UnknownBurst, result.BatchType);
        Assert.True(result.SuppressIndividualPrompts);
        Assert.Equal(51, result.FileCount);
    }

    [Fact]
    public void Batch_MoreThanTwoHundredFilesInFiveMinutesRequiresBatchReviewOnly()
    {
        var result = _detector.Evaluate(Request(@"C:\Intake", count: 201, spacing: TimeSpan.FromSeconds(1)));

        Assert.Equal(BatchPromptDecision.BatchReviewOnly, result.Decision);
        Assert.Equal(EventBatchType.UnknownBurst, result.BatchType);
        Assert.True(result.SuppressIndividualPrompts);
        Assert.Equal(201, result.FileCount);
    }

    [Fact]
    public void Batch_ArchiveExtractionBurstSuppressesIndividualPrompts()
    {
        var result = _detector.Evaluate(Request(
            @"C:\Intake\Extracted Package",
            count: 51,
            spacing: TimeSpan.FromMilliseconds(500),
            relativePathFactory: index => $@"nested\folder{index % 4}\file{index}.txt"));

        Assert.Equal(EventBatchType.ArchiveExtractionBatch, result.BatchType);
        Assert.Equal(BatchPromptDecision.SuppressIndividualPrompts, result.Decision);
        Assert.True(result.SuppressIndividualPrompts);
    }

    [Fact]
    public void Batch_OneDriveSyncBurstSuppressesIndividualPrompts()
    {
        var result = _detector.Evaluate(Request(
            @"C:\Users\User\OneDrive\Downloads",
            count: 51,
            spacing: TimeSpan.FromMilliseconds(500)));

        Assert.Equal(EventBatchType.OneDriveSyncBurst, result.BatchType);
        Assert.Equal(BatchPromptDecision.SuppressIndividualPrompts, result.Decision);
        Assert.True(result.SuppressIndividualPrompts);
    }

    [Fact]
    public void Batch_InstallerOrUnpackerBurstSuppressesIndividualPrompts()
    {
        var result = _detector.Evaluate(Request(
            @"C:\Intake\Installer\resources",
            count: 51,
            spacing: TimeSpan.FromMilliseconds(500),
            relativePathFactory: index => $"module{index}.dll"));

        Assert.Equal(EventBatchType.InstallerOrUnpackerBurst, result.BatchType);
        Assert.Equal(BatchPromptDecision.SuppressIndividualPrompts, result.Decision);
        Assert.True(result.SuppressIndividualPrompts);
    }

    [Fact]
    public void Batch_PackageManagerBurstSuppressesIndividualPrompts()
    {
        var result = _detector.Evaluate(Request(
            @"C:\Intake\Project\node_modules",
            count: 51,
            spacing: TimeSpan.FromMilliseconds(500),
            relativePathFactory: index => $@"package{index}\index.js"));

        Assert.Equal(EventBatchType.PackageInstallNoise, result.BatchType);
        Assert.Equal(BatchPromptDecision.SuppressIndividualPrompts, result.Decision);
        Assert.True(result.SuppressIndividualPrompts);
    }

    [Fact]
    public void Batch_BuildOutputBurstSuppressesIndividualPrompts()
    {
        var result = _detector.Evaluate(Request(
            @"C:\Intake\Project\build",
            count: 51,
            spacing: TimeSpan.FromMilliseconds(500),
            relativePathFactory: index => $"output{index}.dll"));

        Assert.Equal(EventBatchType.BuildOrCompilerNoise, result.BatchType);
        Assert.Equal(BatchPromptDecision.SuppressIndividualPrompts, result.Decision);
        Assert.True(result.SuppressIndividualPrompts);
    }

    [Fact]
    public void Batch_EventsOutsideRootAreIgnored()
    {
        var events = Enumerable.Range(0, 60)
            .Select(index => new FileBatchEvent(
                Path: $@"D:\Other\file{index}.txt",
                RootPath: @"D:\Other",
                ObservedAt: BaseTime.AddMilliseconds(index)))
            .ToArray();

        var result = _detector.Evaluate(new BatchDetectionRequest(
            RootPath: @"C:\Intake",
            Events: events,
            Now: BaseTime.AddSeconds(1)));

        Assert.Equal(BatchPromptDecision.NoBatch, result.Decision);
        Assert.Equal(EventBatchType.None, result.BatchType);
        Assert.False(result.SuppressIndividualPrompts);
        Assert.Equal(0, result.FileCount);
    }

    private static BatchDetectionRequest Request(
        string rootPath,
        int count,
        TimeSpan spacing,
        Func<int, string>? relativePathFactory = null)
    {
        var events = Enumerable.Range(0, count)
            .Select(index =>
            {
                var relativePath = relativePathFactory?.Invoke(index) ?? $"file{index}.txt";
                return new FileBatchEvent(
                    Path: Path.Combine(rootPath, relativePath),
                    RootPath: rootPath,
                    ObservedAt: BaseTime.AddTicks(spacing.Ticks * index));
            })
            .ToArray();

        return new BatchDetectionRequest(
            RootPath: rootPath,
            Events: events,
            Now: events[^1].ObservedAt);
    }
}
