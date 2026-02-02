using XTouchVMBridge.Core.Enums;

namespace XTouchVMBridge.Core.Models;

/// <summary>
/// Konfiguration eines einzelnen Kanals (Name, Typ, Farbe).
/// Wird aus config.json geladen.
/// </summary>
public class ChannelConfig
{
    /// <summary>Anzeigename (max 7 Zeichen für X-Touch Display).</summary>
    public string Name { get; set; } = "       ";

    /// <summary>Typ-Beschreibung des Kanals.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Display-Farbe.</summary>
    public XTouchColor Color { get; set; } = XTouchColor.Off;
}
