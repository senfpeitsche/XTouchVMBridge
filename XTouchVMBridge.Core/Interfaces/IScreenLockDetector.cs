namespace XTouchVMBridge.Core.Interfaces;

/// <summary>
/// Erkennt ob der Windows-Bildschirm gesperrt ist.
/// </summary>
public interface IScreenLockDetector
{
    /// <summary>Ob der Bildschirm aktuell gesperrt ist.</summary>
    bool IsLocked { get; }

    /// <summary>Status prüfen (mit Throttling).</summary>
    bool CheckLockState();
}
