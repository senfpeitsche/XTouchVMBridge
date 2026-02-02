namespace XTouchVMBridge.Core.Enums;

/// <summary>
/// Anzeigemodi für die Encoder-LED-Ringe auf dem X-Touch Extender.
/// </summary>
public enum XTouchEncoderRingMode : byte
{
    /// <summary>Einzelner Punkt zeigt Position an.</summary>
    Dot = 0,

    /// <summary>Pan-Anzeige von der Mitte aus.</summary>
    Pan = 1,

    /// <summary>LEDs füllen sich von links nach rechts.</summary>
    Wrap = 2,

    /// <summary>LEDs breiten sich symmetrisch von der Mitte aus.</summary>
    Spread = 3
}
