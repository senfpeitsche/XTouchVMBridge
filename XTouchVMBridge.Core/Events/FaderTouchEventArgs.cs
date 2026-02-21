namespace XTouchVMBridge.Core.Events;

public class FaderTouchEventArgs : EventArgs
{
    public int Channel { get; }

    public bool IsTouched { get; }

    public TimeSpan TimePressed { get; }

    public FaderTouchEventArgs(int channel, bool isTouched, TimeSpan timePressed = default)
    {
        Channel = channel;
        IsTouched = isTouched;
        TimePressed = timePressed;
    }
}
