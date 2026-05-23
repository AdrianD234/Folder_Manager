using FileIntakeAssistant.App.Shell;
using FileIntakeAssistant.Infrastructure.Logging;

namespace FileIntakeAssistant.Tests.Logging;

public sealed class AppLifecycleAuditTests : IAsyncLifetime
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "FileIntakeAssistant.Tests",
        Guid.NewGuid().ToString("N"));

    private string LogsDirectory => Path.Combine(_testRoot, "File Intake Assistant", "logs");

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        var fullRoot = Path.GetFullPath(_testRoot);
        var allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "FileIntakeAssistant.Tests"));

        if (fullRoot.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullRoot))
        {
            Directory.Delete(fullRoot, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task WriteAsync_WritesLifecycleEventWithoutExposingSecrets()
    {
        var localAuditLog = new JsonLinesLocalAuditLog(LogsDirectory);
        var audit = new AppLifecycleAudit(localAuditLog);

        await audit.WriteAsync(
            "tray_icon.command",
            "Requested",
            new Dictionary<string, object?>
            {
                ["command"] = "open_search",
                ["apiKey"] = "sk-123456789012345678901234567890",
                ["message"] = "startup text sk-abcdefghijklmnopqrstuvwxyz123456"
            });

        var content = await File.ReadAllTextAsync(localAuditLog.LogFilePath);

        Assert.Contains("tray_icon.command", content);
        Assert.Contains("open_search", content);
        Assert.Contains("[redacted]", content);
        Assert.DoesNotContain("sk-123456789012345678901234567890", content);
        Assert.DoesNotContain("sk-abcdefghijklmnopqrstuvwxyz123456", content);
    }

    [Fact]
    public async Task WriteAsync_SwallowsAuditLogFailures()
    {
        var audit = new AppLifecycleAudit(new ThrowingAuditLog());

        await audit.WriteAsync(
            "app.startup",
            "Completed",
            new Dictionary<string, object?>
            {
                ["component"] = "app"
            });
    }

    private sealed class ThrowingAuditLog : ILocalAuditLog
    {
        public Task WriteAsync(
            string eventType,
            string status,
            IReadOnlyDictionary<string, object?> fields,
            CancellationToken cancellationToken = default)
        {
            throw new IOException("Synthetic logging failure.");
        }
    }
}
