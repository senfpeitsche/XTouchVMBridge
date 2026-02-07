using Microsoft.Win32;
using XTouchVMBridge.Core.Interfaces;

namespace XTouchVMBridge.App.Services;

/// <summary>
/// Erkennt ob der Windows-Bildschirm gesperrt ist.
/// Nutzt SystemEvents.SessionSwitch für sofortige, event-basierte Erkennung.
/// </summary>
public class ScreenLockDetector : IScreenLockDetector
{
    private bool _isLocked;

    public bool IsLocked => _isLocked;

    public event EventHandler<bool>? LockStateChanged;

    public ScreenLockDetector()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        bool wasLocked = _isLocked;

        _isLocked = e.Reason switch
        {
            SessionSwitchReason.SessionLock => true,
            SessionSwitchReason.SessionUnlock => false,
            _ => _isLocked
        };

        if (_isLocked != wasLocked)
        {
            LockStateChanged?.Invoke(this, _isLocked);
        }
    }
}
