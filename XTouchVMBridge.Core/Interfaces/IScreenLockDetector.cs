namespace XTouchVMBridge.Core.Interfaces;

/// <summary>
/// Erkennt ob der Windows-Bildschirm gesperrt ist.
/// </summary>
public interface IScreenLockDetector
{
    /// <summary>Ob der Bildschirm aktuell gesperrt ist.</summary>
    bool IsLocked { get; }

    /// <summary>Wird ausgelöst wenn sich der Lock-Status ändert.</summary>
    event EventHandler<bool> LockStateChanged;
}
