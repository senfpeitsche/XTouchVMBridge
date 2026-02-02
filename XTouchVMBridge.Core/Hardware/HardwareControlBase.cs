namespace XTouchVMBridge.Core.Hardware;

/// <summary>
/// Basisklasse für alle Hardware-Controls (Fader, Buttons, Encoder, etc.).
/// Neue Control-Typen erben von dieser Klasse.
/// </summary>
public abstract class HardwareControlBase
{
    /// <summary>Kanal-Index (0-basiert) auf dem physischen Gerät.</summary>
    public int Channel { get; }

    /// <summary>Eindeutiger Name des Controls (z.B. "Fader_0", "Mute_3").</summary>
    public string ControlId { get; }

    protected HardwareControlBase(int channel, string controlId)
    {
        Channel = channel;
        ControlId = controlId;
    }
}
