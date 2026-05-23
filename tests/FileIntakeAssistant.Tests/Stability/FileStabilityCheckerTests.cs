using FileIntakeAssistant.Core.Stability;
using FileIntakeAssistant.Infrastructure.FileSystem;

namespace FileIntakeAssistant.Tests.Stability;

public sealed class FileStabilityCheckerTests : IDisposable
{
    private static readonly DateTimeOffset BaseTime = new(2026, 5, 23, 4, 10, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LastWrite = new(2026, 5, 23, 4, 9, 30, TimeSpan.Zero);

    private readonly FileStabilityChecker _checker = new();
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "FileIntakeAssistant.Tests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        var fullRoot = Path.GetFullPath(_testRoot);
        var allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "FileIntakeAssistant.Tests"));

        if (fullRoot.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullRoot))
        {
            Directory.Delete(fullRoot, recursive: true);
        }
    }

    [Fact]
    public void Stability_StableFileAfterDebounceIsReadyAndHashEligible()
    {
        var decision = _checker.Evaluate(new FileStabilityRequest(
            Observations:
            [
                Observation(BaseTime),
                Observation(BaseTime.AddSeconds(2))
            ],
            Now: BaseTime.AddSeconds(2.1)));

        Assert.True(decision.IsStable);
        Assert.Equal(FileStabilityStatus.Stable, decision.Status);
        Assert.Equal(HashPlan.ComputeSha256, decision.HashPlan);
        Assert.Equal(TimeSpan.FromSeconds(2), decision.RequiredDebounceWindow);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public void Stability_ChangingSizeDelaysProcessing()
    {
        var decision = _checker.Evaluate(new FileStabilityRequest(
            Observations:
            [
                Observation(BaseTime, sizeBytes: 100),
                Observation(BaseTime.AddSeconds(3), sizeBytes: 200)
            ],
            Now: BaseTime.AddSeconds(3)));

        Assert.False(decision.IsStable);
        Assert.Equal(FileStabilityStatus.Changing, decision.Status);
        Assert.Equal(HashPlan.NotReady, decision.HashPlan);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public void Stability_ChangingTimestampDelaysProcessing()
    {
        var decision = _checker.Evaluate(new FileStabilityRequest(
            Observations:
            [
                Observation(BaseTime, lastWriteTimeUtc: LastWrite),
                Observation(BaseTime.AddSeconds(3), lastWriteTimeUtc: LastWrite.AddSeconds(1))
            ],
            Now: BaseTime.AddSeconds(3)));

        Assert.False(decision.IsStable);
        Assert.Equal(FileStabilityStatus.Changing, decision.Status);
        Assert.Equal(HashPlan.NotReady, decision.HashPlan);
    }

    [Fact]
    public void Stability_LockedFileDelaysProcessing()
    {
        var decision = _checker.Evaluate(new FileStabilityRequest(
            Observations:
            [
                Observation(BaseTime, isLocked: true),
                Observation(BaseTime.AddSeconds(3), isLocked: true)
            ],
            Now: BaseTime.AddSeconds(3)));

        Assert.False(decision.IsStable);
        Assert.Equal(FileStabilityStatus.Locked, decision.Status);
        Assert.Equal(HashPlan.NotReady, decision.HashPlan);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public void Stability_ZeroByteTransientDelaysProcessing()
    {
        var decision = _checker.Evaluate(new FileStabilityRequest(
            Observations:
            [
                Observation(BaseTime, sizeBytes: 0),
                Observation(BaseTime.AddSeconds(3), sizeBytes: 0)
            ],
            Now: BaseTime.AddSeconds(3)));

        Assert.False(decision.IsStable);
        Assert.Equal(FileStabilityStatus.ZeroByteTransient, decision.Status);
        Assert.Equal(HashPlan.NotReady, decision.HashPlan);
    }

    [Fact]
    public void Stability_PartialExtensionDelaysProcessing()
    {
        var decision = _checker.Evaluate(new FileStabilityRequest(
            Observations:
            [
                Observation(BaseTime, path: @"C:\Intake\Report.pdf.crdownload"),
                Observation(BaseTime.AddSeconds(3), path: @"C:\Intake\Report.pdf.crdownload")
            ],
            Now: BaseTime.AddSeconds(3)));

        Assert.False(decision.IsStable);
        Assert.Equal(FileStabilityStatus.PartialOrTemporary, decision.Status);
        Assert.Equal(HashPlan.NotReady, decision.HashPlan);
    }

    [Fact]
    public void Stability_ExtendedDebounceAppliesAfterPartialTransition()
    {
        var waiting = _checker.Evaluate(new FileStabilityRequest(
            Observations:
            [
                Observation(BaseTime, requiresExtendedDebounce: true),
                Observation(BaseTime.AddSeconds(5), requiresExtendedDebounce: true)
            ],
            Now: BaseTime.AddSeconds(5)));

        var stable = _checker.Evaluate(new FileStabilityRequest(
            Observations:
            [
                Observation(BaseTime, requiresExtendedDebounce: true),
                Observation(BaseTime.AddSeconds(10), requiresExtendedDebounce: true)
            ],
            Now: BaseTime.AddSeconds(10.5)));

        Assert.False(waiting.IsStable);
        Assert.Equal(FileStabilityStatus.WaitingForDebounce, waiting.Status);
        Assert.Equal(TimeSpan.FromSeconds(10), waiting.RequiredDebounceWindow);
        Assert.True(stable.IsStable);
        Assert.Equal(FileStabilityStatus.Stable, stable.Status);
    }

    [Fact]
    public void Stability_LargeStableFileDefersHash()
    {
        var options = FileStabilityOptions.Defaults with { HashThresholdBytes = 1_024 };

        var decision = _checker.Evaluate(new FileStabilityRequest(
            Observations:
            [
                Observation(BaseTime, sizeBytes: 2_048),
                Observation(BaseTime.AddSeconds(3), sizeBytes: 2_048)
            ],
            Now: BaseTime.AddSeconds(3),
            Options: options));

        Assert.True(decision.IsStable);
        Assert.Equal(FileStabilityStatus.Stable, decision.Status);
        Assert.Equal(HashPlan.DeferLargeFile, decision.HashPlan);
        Assert.Contains("deferred", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stability_LocalSnapshotReaderDetectsLockedTempFile()
    {
        Directory.CreateDirectory(_testRoot);
        var path = Path.Combine(_testRoot, "Locked.pdf");
        await File.WriteAllTextAsync(path, "placeholder");

        await using var lockedStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var snapshot = await new LocalFileStabilitySnapshotReader().ReadAsync(path, BaseTime);
        var decision = _checker.Evaluate(new FileStabilityRequest(
            Observations: [snapshot, snapshot with { ObservedAt = BaseTime.AddSeconds(3) }],
            Now: BaseTime.AddSeconds(3)));

        Assert.True(snapshot.Exists);
        Assert.True(snapshot.IsLocked);
        Assert.False(decision.IsStable);
        Assert.Equal(FileStabilityStatus.Locked, decision.Status);
    }

    [Fact]
    public async Task Stability_LocalSnapshotReaderSupportsTempCopiedFileSimulation()
    {
        Directory.CreateDirectory(_testRoot);
        var path = Path.Combine(_testRoot, "Copied.pdf");
        await File.WriteAllTextAsync(path, "placeholder");

        var reader = new LocalFileStabilitySnapshotReader();
        var first = await reader.ReadAsync(path, BaseTime);
        var second = await reader.ReadAsync(path, BaseTime.AddSeconds(3));

        var decision = _checker.Evaluate(new FileStabilityRequest(
            Observations: [first, second],
            Now: BaseTime.AddSeconds(3)));

        Assert.True(first.Exists);
        Assert.False(first.IsLocked);
        Assert.True(decision.IsStable);
        Assert.Equal(FileStabilityStatus.Stable, decision.Status);
    }

    private static FileStabilityObservation Observation(
        DateTimeOffset observedAt,
        string path = @"C:\Intake\Report.pdf",
        bool exists = true,
        bool isDirectory = false,
        long sizeBytes = 1_024,
        DateTimeOffset? lastWriteTimeUtc = null,
        bool isLocked = false,
        bool requiresExtendedDebounce = false)
    {
        return new FileStabilityObservation(
            Path: path,
            Exists: exists,
            IsDirectory: isDirectory,
            SizeBytes: sizeBytes,
            LastWriteTimeUtc: lastWriteTimeUtc ?? LastWrite,
            IsLocked: isLocked,
            ObservedAt: observedAt,
            RequiresExtendedDebounce: requiresExtendedDebounce);
    }
}
