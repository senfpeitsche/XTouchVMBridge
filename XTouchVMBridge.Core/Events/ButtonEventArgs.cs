using XTouchVMBridge.Core.Enums;

namespace XTouchVMBridge.Core.Events;

/// <summary>
/// Event-Daten wenn ein Button gedrückt oder losgelassen wird.
/// </summary>
public class ButtonEventArgs : EventArgs
{
    /// <summary>Kanal-Index (0–7).</summary>
    public int Channel { get; }

    /// <summary>Typ des Buttons.</summary>
    public XTouchButtonType ButtonType { get; }

    /// <summary>True = gedrückt, False = losgelassen.</summary>
    public bool IsPressed { get; }

    /// <summary>Wie lange der Button gedrückt war (nur bei Release relevant).</summary>
    public TimeSpan TimePressed { get; }

    public ButtonEventArgs(int channel, XTouchButtonType buttonType, bool isPressed, TimeSpan timePressed = default)
    {
        Channel = channel;
        ButtonType = buttonType;
        IsPressed = isPressed;
        TimePressed = timePressed;
    }
}
