using XTouchVMBridge.Core.Enums;

namespace XTouchVMBridge.Core.Hardware;

public class ButtonControl : HardwareControlBase
{
    private LedState _ledState;
    private bool _isPressed;

    public XTouchButtonType ButtonType { get; }

    public int NoteNumber { get; }

    public ButtonControl(int channel, XTouchButtonType buttonType)
        : base(channel, $"{buttonType}_{channel}")
    {
        ButtonType = buttonType;
        NoteNumber = (int)buttonType * 8 + channel;
    }

    public LedState LedState
    {
        get => _ledState;
        set => _ledState = value;
    }

    public bool IsPressed
    {
        get => _isPressed;
        set => _isPressed = value;
    }
}
