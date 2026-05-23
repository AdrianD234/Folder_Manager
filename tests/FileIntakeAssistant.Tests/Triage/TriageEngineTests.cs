using FileIntakeAssistant.Core.Triage;

namespace FileIntakeAssistant.Tests.Triage;

public sealed class TriageEngineTests
{
    private static readonly DateTimeOffset ObservedAt = new(2026, 5, 23, 4, 0, 0, TimeSpan.Zero);

    private readonly FileEventTriageEngine _engine = new();

    [Theory]
    [InlineData(@"C:\Intake\Report.pdf.crdownload")]
    [InlineData(@"C:\Intake\Report.pdf.part")]
    [InlineData(@"C:\Intake\Report.tmp")]
    [InlineData(@"C:\Intake\Report.download")]
    [InlineData(@"C:\Intake\Report.partial")]
    [InlineData(@"C:\Intake\~$Budget.xlsx")]
    [InlineData(@"C:\Intake\notes.lock")]
    [InlineData(@"C:\Intake\notes.swp")]
    public void Triage_TemporaryAndPartialDownloadsAreDelayed(string path)
    {
        var decision = _engine.Evaluate(Request(path));

        Assert.Equal(TriageCategory.TemporaryOrPartial, decision.Category);
        Assert.Equal(ProcessingState.WaitingForStability, decision.ProcessingState);
        Assert.False(decision.PromptAllowed);
        Assert.InRange(decision.Confidence, 0.9, 1.0);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Theory]
    [InlineData(@".git\config", TriageCategory.DevelopmentNoise)]
    [InlineData(@"node_modules\package\index.js", TriageCategory.PackageInstallNoise)]
    [InlineData(@".venv\Lib\site-packages\package.py", TriageCategory.DevelopmentNoise)]
    [InlineData(@"venv\Scripts\python.exe", TriageCategory.DevelopmentNoise)]
    [InlineData(@"bin\Debug\net8.0\App.dll", TriageCategory.BuildOrCompilerNoise)]
    [InlineData(@"obj\Debug\app.g.cs", TriageCategory.BuildOrCompilerNoise)]
    [InlineData(@"target\classes\App.class", TriageCategory.BuildOrCompilerNoise)]
    [InlineData(@"dist\bundle.min.js", TriageCategory.BuildOrCompilerNoise)]
    [InlineData(@"build\output.dll", TriageCategory.BuildOrCompilerNoise)]
    [InlineData(@".vs\FileContentIndex\cache.dat", TriageCategory.DevelopmentNoise)]
    [InlineData(@".idea\workspace.xml", TriageCategory.DevelopmentNoise)]
    public void Triage_DevelopmentRepoBuildAndPackageFoldersAreSuppressed(
        string relativePath,
        TriageCategory expectedCategory)
    {
        var decision = _engine.Evaluate(Request($@"C:\Intake\Project\{relativePath}"));

        Assert.Equal(expectedCategory, decision.Category);
        Assert.Equal(ProcessingState.Ignored, decision.ProcessingState);
        Assert.False(decision.PromptAllowed);
        Assert.Equal(FolderContextRecommendation.PreferFolderContext, decision.FolderContextRecommendation);
        Assert.InRange(decision.Confidence, 0.9, 1.0);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Theory]
    [InlineData(@"C:\Windows\Temp\Report.pdf", TriageCategory.SystemOrAppDataNoise)]
    [InlineData(@"C:\Program Files\Vendor\Report.pdf", TriageCategory.SystemOrAppDataNoise)]
    [InlineData(@"C:\Program Files (x86)\Vendor\Report.pdf", TriageCategory.SystemOrAppDataNoise)]
    [InlineData(@"C:\Users\User\AppData\Local\Vendor\Report.pdf", TriageCategory.SystemOrAppDataNoise)]
    [InlineData(@"C:\Users\User\AppData\Local\File Intake Assistant\data\file-intake.db", TriageCategory.SystemOrAppDataNoise)]
    [InlineData(@"C:\Users\User\AppData\Local\Google\Chrome\User Data\Default\Cache\f_000123", TriageCategory.BrowserCacheNoise)]
    [InlineData(@"C:\Users\User\AppData\Local\Microsoft\Edge\User Data\Default\Code Cache\js\file", TriageCategory.BrowserCacheNoise)]
    [InlineData(@"C:\Users\User\AppData\Local\Mozilla\Firefox\Profiles\abc.default\cache2\entries\file", TriageCategory.BrowserCacheNoise)]
    public void Triage_SystemAppDataAndBrowserCachePathsAreSuppressed(
        string path,
        TriageCategory expectedCategory)
    {
        var decision = _engine.Evaluate(Request(path));

        Assert.Equal(expectedCategory, decision.Category);
        Assert.Equal(ProcessingState.Ignored, decision.ProcessingState);
        Assert.False(decision.PromptAllowed);
        Assert.InRange(decision.Confidence, 0.9, 1.0);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Theory]
    [InlineData("Report.pdf")]
    [InlineData("Budget.xlsx")]
    [InlineData("Brief.docx")]
    [InlineData("Slides.pptx")]
    [InlineData("Screenshot.png")]
    [InlineData("Archive.zip")]
    [InlineData("Notes.txt")]
    [InlineData("Readme.md")]
    [InlineData("Data.csv")]
    public void Triage_MeaningfulStableUserFilesBecomeCandidates(string fileName)
    {
        var decision = _engine.Evaluate(Request($@"C:\Intake\{fileName}"));

        Assert.Equal(TriageCategory.MeaningfulOneOff, decision.Category);
        Assert.Equal(ProcessingState.Candidate, decision.ProcessingState);
        Assert.True(decision.PromptAllowed);
        Assert.Equal(FolderContextRecommendation.None, decision.FolderContextRecommendation);
        Assert.InRange(decision.Confidence, 0.85, 1.0);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public void Triage_UnstableMeaningfulFileWaitsForStability()
    {
        var decision = _engine.Evaluate(Request(@"C:\Intake\Report.pdf", isStable: false));

        Assert.Equal(TriageCategory.NeedsMoreObservation, decision.Category);
        Assert.Equal(ProcessingState.WaitingForStability, decision.ProcessingState);
        Assert.False(decision.PromptAllowed);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public void Triage_OutsideEnabledIntakeFolderIsIgnored()
    {
        var decision = _engine.Evaluate(Request(@"D:\Other\Report.pdf", isUnderEnabledIntakeFolder: false));

        Assert.Equal(TriageCategory.UnknownSafeToIgnore, decision.Category);
        Assert.Equal(ProcessingState.Ignored, decision.ProcessingState);
        Assert.False(decision.PromptAllowed);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public void Triage_OwnOperationPathIsSuppressedInsideWindow()
    {
        var operation = new OwnOperationSuppression(
            OldPath: @"C:\Intake\Report.pdf",
            NewPath: @"C:\Filed\Report.pdf",
            RegisteredAt: ObservedAt.AddSeconds(-10),
            SuppressionWindow: OwnOperationSuppression.DefaultSuppressionWindow);

        var decision = _engine.Evaluate(Request(@"C:\Filed\Report.pdf", ownOperations: [operation]));

        Assert.Equal(TriageCategory.OwnOperation, decision.Category);
        Assert.Equal(ProcessingState.Ignored, decision.ProcessingState);
        Assert.False(decision.PromptAllowed);
        Assert.Equal(FolderContextRecommendation.None, decision.FolderContextRecommendation);
        Assert.InRange(decision.Confidence, 0.95, 1.0);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public void Triage_OwnOperationPathDoesNotSuppressAfterWindow()
    {
        var operation = new OwnOperationSuppression(
            OldPath: @"C:\Intake\Report.pdf",
            NewPath: @"C:\Filed\Report.pdf",
            RegisteredAt: ObservedAt.AddMinutes(-5),
            SuppressionWindow: OwnOperationSuppression.DefaultSuppressionWindow);

        var decision = _engine.Evaluate(Request(@"C:\Filed\Report.pdf", ownOperations: [operation]));

        Assert.Equal(TriageCategory.MeaningfulOneOff, decision.Category);
        Assert.True(decision.PromptAllowed);
    }

    [Fact]
    public void Triage_OwnOperationDoesNotSuppressUnrelatedPath()
    {
        var operation = new OwnOperationSuppression(
            OldPath: @"C:\Intake\Report.pdf",
            NewPath: @"C:\Filed\Report.pdf",
            RegisteredAt: ObservedAt.AddSeconds(-10),
            SuppressionWindow: OwnOperationSuppression.DefaultSuppressionWindow);

        var decision = _engine.Evaluate(Request(@"C:\Filed\Different.pdf", ownOperations: [operation]));

        Assert.Equal(TriageCategory.MeaningfulOneOff, decision.Category);
        Assert.True(decision.PromptAllowed);
    }

    [Fact]
    public void Triage_UnknownStableFileIsIgnoredWithoutPrompt()
    {
        var decision = _engine.Evaluate(Request(@"C:\Intake\unknown.customext"));

        Assert.Equal(TriageCategory.UnknownSafeToIgnore, decision.Category);
        Assert.Equal(ProcessingState.Ignored, decision.ProcessingState);
        Assert.False(decision.PromptAllowed);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public void Triage_DeletedMeaningfulFileIsIgnoredWithoutPrompt()
    {
        var decision = _engine.Evaluate(Request(@"C:\Intake\Report.pdf", eventKind: FileEventKind.Deleted));

        Assert.Equal(TriageCategory.UnknownSafeToIgnore, decision.Category);
        Assert.Equal(ProcessingState.Ignored, decision.ProcessingState);
        Assert.False(decision.PromptAllowed);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public void Triage_DirectoryWithMeaningfulExtensionIsNotAFileCandidate()
    {
        var decision = _engine.Evaluate(Request(@"C:\Intake\Archive.zip", isDirectory: true));

        Assert.Equal(TriageCategory.UnknownSafeToIgnore, decision.Category);
        Assert.Equal(ProcessingState.Ignored, decision.ProcessingState);
        Assert.False(decision.PromptAllowed);
        Assert.Equal(FolderContextRecommendation.PreferFolderContext, decision.FolderContextRecommendation);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    private static TriageRequest Request(
        string path,
        FileEventKind eventKind = FileEventKind.Created,
        bool isDirectory = false,
        bool isStable = true,
        bool isUnderEnabledIntakeFolder = true,
        IReadOnlyCollection<OwnOperationSuppression>? ownOperations = null)
    {
        return new TriageRequest(
            Path: path,
            EventKind: eventKind,
            IsDirectory: isDirectory,
            IsUnderEnabledIntakeFolder: isUnderEnabledIntakeFolder,
            IsStable: isStable,
            ObservedAt: ObservedAt,
            OwnOperations: ownOperations);
    }
}
