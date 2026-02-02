using XTouchVMBridge.Core.Enums;

namespace XTouchVMBridge.Core.Models;

/// <summary>
/// Definiert eine Kanal-Ansicht (Channel View).
/// Jede View mappt 8 physische X-Touch-Kanäle auf logische VM-Kanäle (0–15).
/// Optional kann ein VM-Kanal für den Main Fader zugewiesen werden.
/// </summary>
public class ChannelViewConfig
{
    /// <summary>Name der Ansicht (max 7 Zeichen, wird auf dem Display angezeigt).</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 8 VM-Kanal-Indizes (0–15), einer pro X-Touch-Strip.
    /// Index 0 = X-Touch Kanal 1, Index 7 = X-Touch Kanal 8.
    /// </summary>
    public int[] Channels { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Optionale Display-Farben pro Strip (8 Einträge, null = Farbe aus globaler Channel-Config).
    /// Index 0 = X-Touch Kanal 1, Index 7 = X-Touch Kanal 8.
    /// </summary>
    public XTouchColor?[]? ChannelColors { get; set; }

    /// <summary>
    /// VM-Kanal-Index für den Main Fader (optional).
    /// Wenn null, steuert der Main Fader keinen VM-Parameter.
    /// </summary>
    public int? MainFaderChannel { get; set; }

    /// <summary>
    /// Gibt die Farbe für einen X-Touch-Strip zurück.
    /// Wenn in der View eine Farbe gesetzt ist, wird diese verwendet, sonst null (= globale Farbe).
    /// </summary>
    public XTouchColor? GetChannelColor(int stripIndex)
    {
        if (ChannelColors == null || stripIndex < 0 || stripIndex >= ChannelColors.Length)
            return null;
        return ChannelColors[stripIndex];
    }
}
