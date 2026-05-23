using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FileIntakeAssistant.App.Shell;

public sealed class GlobalHotkeyController : IDisposable
{
    private const int HotkeyId = 0x4649;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint VirtualKeyF = 0x46;

    private readonly Window _window;
    private readonly Action _activated;
    private readonly AppLifecycleAudit? _audit;
    private HwndSource? _source;
    private IntPtr _windowHandle;
    private bool _disposed;

    public GlobalHotkeyController(Window window, Action activated, AppLifecycleAudit? audit = null)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _activated = activated ?? throw new ArgumentNullException(nameof(activated));
        _audit = audit;
        _window.SourceInitialized += OnSourceInitialized;
    }

    public bool IsRegistered { get; private set; }

    public int? RegistrationError { get; private set; }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _window.SourceInitialized -= OnSourceInitialized;
        _windowHandle = new WindowInteropHelper(_window).Handle;
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(WndProc);

        IsRegistered = RegisterHotKey(_windowHandle, HotkeyId, ModControl | ModAlt, VirtualKeyF);
        if (!IsRegistered)
        {
            RegistrationError = Marshal.GetLastWin32Error();
        }

        var fields = new Dictionary<string, object?>
        {
            ["component"] = "global_hotkey",
            ["hotkey"] = "Ctrl+Alt+F",
            ["registered"] = IsRegistered
        };
        if (RegistrationError is not null)
        {
            fields["registrationError"] = RegistrationError.Value;
        }

        _audit?.WriteFireAndForget(
            "global_hotkey.registration",
            IsRegistered ? "Completed" : "Failed",
            fields);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            _audit?.WriteFireAndForget(
                "global_hotkey.activated",
                "Requested",
                new Dictionary<string, object?>
                {
                    ["hotkey"] = "Ctrl+Alt+F"
                });

            try
            {
                _activated();
                _audit?.WriteFireAndForget(
                    "global_hotkey.activated",
                    "Completed",
                    new Dictionary<string, object?>
                    {
                        ["hotkey"] = "Ctrl+Alt+F"
                    });
            }
            catch (Exception ex)
            {
                _audit?.WriteFireAndForget(
                    "global_hotkey.activated",
                    "Failed",
                    new Dictionary<string, object?>
                    {
                        ["hotkey"] = "Ctrl+Alt+F",
                        ["errorType"] = ex.GetType().Name
                    });
                throw;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }

        if (IsRegistered && _windowHandle != IntPtr.Zero)
        {
            var unregistered = UnregisterHotKey(_windowHandle, HotkeyId);
            var fields = new Dictionary<string, object?>
            {
                ["component"] = "global_hotkey",
                ["hotkey"] = "Ctrl+Alt+F",
                ["wasRegistered"] = true,
                ["unregistered"] = unregistered
            };
            if (!unregistered)
            {
                fields["unregistrationError"] = Marshal.GetLastWin32Error();
            }

            _audit?.WriteFireAndForget(
                "global_hotkey.disposed",
                unregistered ? "Completed" : "Failed",
                fields);
        }
        else
        {
            _audit?.WriteFireAndForget(
                "global_hotkey.disposed",
                "Skipped",
                new Dictionary<string, object?>
                {
                    ["component"] = "global_hotkey",
                    ["hotkey"] = "Ctrl+Alt+F",
                    ["wasRegistered"] = false
                });
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
