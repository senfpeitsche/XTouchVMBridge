using XTouchVMBridge.Core.Hardware;

namespace XTouchVMBridge.Core.Models;

/// <summary>
/// Fasst alle Hardware-Controls eines X-Touch-Kanals zusammen.
/// Jeder Kanal hat: Display, Fader, 4 Buttons, Encoder, Level-Meter.
/// Neue Controls können hier einfach hinzugefügt werden.
/// </summary>
public class XTouchChannel
{
    /// <summary>Kanal-Index (0–7).</summary>
    public int Index { get; }

    /// <summary>LCD-Display (7 Zeichen × 2 Zeilen + Farbe).</summary>
    public DisplayControl Display { get; }

    /// <summary>Motorisierter Fader.</summary>
    public FaderControl Fader { get; }

    /// <summary>Drehencoder mit Push und LED-Ring.</summary>
    public EncoderControl Encoder { get; }

    /// <summary>Level-Meter-Anzeige.</summary>
    public LevelMeterControl LevelMeter { get; }

    /// <summary>
    /// Alle Buttons des Kanals, indiziert nach ButtonType.
    /// Neue Button-Typen können hier ergänzt werden.
    /// </summary>
    public IReadOnlyDictionary<Enums.XTouchButtonType, ButtonControl> Buttons { get; }

    public XTouchChannel(int index)
    {
        Index = index;
        Display = new DisplayControl(index);
        Fader = new FaderControl(index);
        Encoder = new EncoderControl(index);
        LevelMeter = new LevelMeterControl(index);

        // Alle Button-Typen registrieren — erweiterbar über das Enum
        var buttons = new Dictionary<Enums.XTouchButtonType, ButtonControl>();
        foreach (var buttonType in Enum.GetValues<Enums.XTouchButtonType>())
        {
            buttons[buttonType] = new ButtonControl(index, buttonType);
        }
        Buttons = buttons;
    }

    /// <summary>Shortcut-Zugriff auf einen Button nach Typ.</summary>
    public ButtonControl GetButton(Enums.XTouchButtonType type) => Buttons[type];
}
