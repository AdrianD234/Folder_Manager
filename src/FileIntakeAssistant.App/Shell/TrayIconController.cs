using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace FileIntakeAssistant.App.Shell;

public sealed class TrayIconController : IDisposable
{
    private readonly Window _window;
    private readonly Action _showIntake;
    private readonly Action _showSearch;
    private readonly Action _exit;
    private readonly AppLifecycleAudit? _audit;
    private readonly Forms.NotifyIcon _notifyIcon;
    private bool _disposed;

    public TrayIconController(
        Window window,
        Action showIntake,
        Action showSearch,
        Action exit,
        AppLifecycleAudit? audit = null)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _showIntake = showIntake ?? throw new ArgumentNullException(nameof(showIntake));
        _showSearch = showSearch ?? throw new ArgumentNullException(nameof(showSearch));
        _exit = exit ?? throw new ArgumentNullException(nameof(exit));
        _audit = audit;

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => Dispatch("open_intake", _showIntake));
        menu.Items.Add("Search", null, (_, _) => Dispatch("open_search", _showSearch));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Dispatch("exit", _exit));

        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = SystemIcons.Application,
            Text = "File Intake Assistant",
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => Dispatch("double_click_open_intake", _showIntake);
        _audit?.WriteFireAndForget(
            "tray_icon.created",
            "Completed",
            new Dictionary<string, object?>
            {
                ["component"] = "tray_icon",
                ["visible"] = true
            });
    }

    private void Dispatch(string command, Action action)
    {
        _audit?.WriteFireAndForget(
            "tray_icon.command",
            "Requested",
            new Dictionary<string, object?>
            {
                ["command"] = command
            });

        try
        {
            if (_window.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _window.Dispatcher.Invoke(action);
            }

            _audit?.WriteFireAndForget(
                "tray_icon.command",
                "Completed",
                new Dictionary<string, object?>
                {
                    ["command"] = command
                });
        }
        catch (Exception ex)
        {
            _audit?.WriteFireAndForget(
                "tray_icon.command",
                "Failed",
                new Dictionary<string, object?>
                {
                    ["command"] = command,
                    ["errorType"] = ex.GetType().Name
                });
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _audit?.WriteFireAndForget(
            "tray_icon.disposed",
            "Completed",
            new Dictionary<string, object?>
            {
                ["component"] = "tray_icon"
            });
    }
}
