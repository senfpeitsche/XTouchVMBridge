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
    /// VM-Kanal-Index für den Main Fader (optional).
    /// Wenn null, steuert der Main Fader keinen VM-Parameter.
    /// </summary>
    public int? MainFaderChannel { get; set; }
}
