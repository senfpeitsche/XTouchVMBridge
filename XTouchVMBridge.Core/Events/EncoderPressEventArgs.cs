namespace XTouchVMBridge.Core.Events;

public class EncoderPressEventArgs : EventArgs
{
    public int Channel { get; }

    public bool IsPressed { get; }

    public TimeSpan TimePressed { get; }

    public EncoderPressEventArgs(int channel, bool isPressed, TimeSpan timePressed = default)
    {
        Channel = channel;
        IsPressed = isPressed;
        TimePressed = timePressed;
    }
}
