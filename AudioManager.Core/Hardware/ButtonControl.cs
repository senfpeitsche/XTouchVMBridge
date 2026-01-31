using AudioManager.Core.Enums;

namespace AudioManager.Core.Hardware;

/// <summary>
/// Repräsentiert einen Button mit LED-Feedback.
/// Kann für beliebige Button-Typen (Rec, Solo, Mute, Select, oder zukünftige) verwendet werden.
/// </summary>
public class ButtonControl : HardwareControlBase
{
    private LedState _ledState;
    private bool _isPressed;

    /// <summary>Typ des Buttons (Rec, Solo, Mute, Select).</summary>
    public XTouchButtonType ButtonType { get; }

    /// <summary>MIDI Note-Number für diesen Button.</summary>
    public int NoteNumber { get; }

    public ButtonControl(int channel, XTouchButtonType buttonType)
        : base(channel, $"{buttonType}_{channel}")
    {
        ButtonType = buttonType;
        NoteNumber = (int)buttonType * 8 + channel;
    }

    /// <summary>Aktueller LED-Zustand (Off/On/Blink).</summary>
    public LedState LedState
    {
        get => _ledState;
        set => _ledState = value;
    }

    /// <summary>Ob der Button gerade gedrückt ist.</summary>
    public bool IsPressed
    {
        get => _isPressed;
        set => _isPressed = value;
    }
}
