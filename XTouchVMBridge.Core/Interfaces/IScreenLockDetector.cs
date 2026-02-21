namespace XTouchVMBridge.Core.Interfaces;

public interface IScreenLockDetector
{
    bool IsLocked { get; }

    event EventHandler<bool> LockStateChanged;
}
