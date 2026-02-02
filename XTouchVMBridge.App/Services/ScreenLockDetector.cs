using System.Diagnostics;
using System.Runtime.InteropServices;
using XTouchVMBridge.Core.Interfaces;

namespace XTouchVMBridge.App.Services;

/// <summary>
/// Erkennt ob der Windows-Bildschirm gesperrt ist.
/// Entspricht islocked.py aus dem Python-Original.
///
/// Prüft über GetForegroundWindow → GetWindowThreadProcessId → Prozessname
/// ob LockApp.exe der Vordergrund-Prozess ist.
/// </summary>
public class ScreenLockDetector : IScreenLockDetector
{
    private bool _isLocked;
    private DateTime _nextCheck = DateTime.MinValue;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(1);

    public bool IsLocked => _isLocked;

    public bool CheckLockState()
    {
        if (DateTime.UtcNow < _nextCheck)
            return _isLocked;

        _nextCheck = DateTime.UtcNow + _checkInterval;

        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                _isLocked = false;
                return _isLocked;
            }

            GetWindowThreadProcessId(hwnd, out uint processId);
            if (processId == 0)
            {
                _isLocked = false;
                return _isLocked;
            }

            using var process = Process.GetProcessById((int)processId);
            string? processName = process.ProcessName;
            _isLocked = processName != null &&
                        processName.Contains("LockApp", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Bei AccessDenied oder ungültigem Prozess: nicht als gesperrt behandeln
            _isLocked = false;
        }

        return _isLocked;
    }

    // ─── P/Invoke ───────────────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
