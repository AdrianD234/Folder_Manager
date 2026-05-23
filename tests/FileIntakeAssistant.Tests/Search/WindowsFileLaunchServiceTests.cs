using System.Diagnostics;
using FileIntakeAssistant.App.ViewModels;

namespace FileIntakeAssistant.Tests.Search;

public sealed class WindowsFileLaunchServiceTests : IDisposable
{
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

    [Theory]
    [InlineData("")]
    [InlineData("relative-file.pdf")]
    [InlineData("https://example.test/report.pdf")]
    [InlineData("http://example.test/report.pdf")]
    [InlineData("file:///C:/Temp/report.pdf")]
    [InlineData("shell:AppsFolder")]
    [InlineData("ms-settings:privacy")]
    [InlineData("C:\\Temp\\report.pdf:Zone.Identifier")]
    public async Task FileLaunch_RejectsNonLocalOrUnsafeTargetsWithoutStartingProcess(string path)
    {
        var startCalls = new List<ProcessStartInfo>();
        var service = new WindowsFileLaunchService(info =>
        {
            startCalls.Add(info);
            return null;
        });

        var fileResult = await service.OpenFileAsync(path);
        var folderResult = await service.OpenFolderAsync(path);

        Assert.False(fileResult.Succeeded);
        Assert.False(folderResult.Succeeded);
        Assert.Empty(startCalls);
    }

    [Fact]
    public async Task FileLaunch_RejectsNonexistentFileAndFolderWithoutStartingProcess()
    {
        var startCalls = new List<ProcessStartInfo>();
        var service = new WindowsFileLaunchService(info =>
        {
            startCalls.Add(info);
            return null;
        });
        var missingFile = Path.Combine(_testRoot, "missing.pdf");
        var missingFolder = Path.Combine(_testRoot, "missing-folder");

        var fileResult = await service.OpenFileAsync(missingFile);
        var folderResult = await service.OpenFolderAsync(missingFolder);

        Assert.False(fileResult.Succeeded);
        Assert.False(folderResult.Succeeded);
        Assert.Empty(startCalls);
    }

    [Fact]
    public async Task FileLaunch_OpenFileStartsOnlyExistingFilePath()
    {
        Directory.CreateDirectory(_testRoot);
        var filePath = Path.Combine(_testRoot, "report.pdf");
        await File.WriteAllTextAsync(filePath, "placeholder");
        var startCalls = new List<ProcessStartInfo>();
        var service = new WindowsFileLaunchService(info =>
        {
            startCalls.Add(info);
            return null;
        });

        var result = await service.OpenFileAsync(filePath);

        Assert.True(result.Succeeded, result.FailureReason);
        var info = Assert.Single(startCalls);
        Assert.Equal(Path.GetFullPath(filePath), info.FileName);
        Assert.True(info.UseShellExecute);
    }

    [Fact]
    public async Task FileLaunch_OpenFolderStartsOnlyExistingDirectoryPath()
    {
        Directory.CreateDirectory(_testRoot);
        var startCalls = new List<ProcessStartInfo>();
        var service = new WindowsFileLaunchService(info =>
        {
            startCalls.Add(info);
            return null;
        });

        var result = await service.OpenFolderAsync(_testRoot);

        Assert.True(result.Succeeded, result.FailureReason);
        var info = Assert.Single(startCalls);
        Assert.Equal(Path.GetFullPath(_testRoot), info.FileName);
        Assert.True(info.UseShellExecute);
    }
}
