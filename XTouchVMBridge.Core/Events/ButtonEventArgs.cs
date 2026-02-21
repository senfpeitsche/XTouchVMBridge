using XTouchVMBridge.Core.Enums;

namespace XTouchVMBridge.Core.Events;

public class ButtonEventArgs : EventArgs
{
    public int Channel { get; }

    public XTouchButtonType ButtonType { get; }

    public bool IsPressed { get; }

    public TimeSpan TimePressed { get; }

    public ButtonEventArgs(int channel, XTouchButtonType buttonType, bool isPressed, TimeSpan timePressed = default)
    {
        Channel = channel;
        ButtonType = buttonType;
        IsPressed = isPressed;
        TimePressed = timePressed;
    }
}
