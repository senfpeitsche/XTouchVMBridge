namespace XTouchVMBridge.Core.Models;

/// <summary>
/// Gesamtkonfiguration der Anwendung.
/// Wird via IOptions{T} per Dependency Injection bereitgestellt.
/// </summary>
public class XTouchVMBridgeConfig
{
    public const string SectionName = "XTouchVMBridge";
    public int ConfigVersion { get; set; }

    /// <summary>Kanal-Konfigurationen (Key = Kanal-Index 0–15).</summary>
    public Dictionary<int, ChannelConfig> Channels { get; set; } = new();

    /// <summary>Voicemeeter API-Typ ("potato", "banana", "basic").</summary>
    public string VoicemeeterApiType { get; set; } = "potato";

    /// <summary>
    /// Optionaler Pfad zu VoicemeeterRemote64.dll oder zum Voicemeeter-Installationsordner.
    /// Wenn gesetzt, wird dieser Pfad beim Start zuerst verwendet.
    /// </summary>
    public string? VoicemeeterDllPath { get; set; }

    /// <summary>Intervall in ms für das Device-Monitoring.</summary>
    public int DeviceMonitorIntervalMs { get; set; } = 5000;

    /// <summary>Ob die XTouch-Integration aktiviert ist.</summary>
    public bool EnableXTouch { get; set; } = true;

    /// <summary>
    /// Control-Mappings pro VM-Kanal (Key = Kanal-Index 0–15).
    /// Definiert welcher Voicemeeter-Parameter auf welches X-Touch-Control gemappt ist.
    /// </summary>
    public Dictionary<int, ControlMappingConfig> Mappings { get; set; } = new();

    /// <summary>
    /// Kanal-Ansichten (Channel Views).
    /// Jede View mappt 8 X-Touch-Strips auf VM-Kanäle + optional Main Fader.
    /// Zwischen Views wird mit Encoder 0 gewechselt.
    /// </summary>
    public List<ChannelViewConfig> ChannelViews { get; set; } = new();

    /// <summary>
    /// Aktionen für Master-Section-Buttons (Key = MIDI Note Number).
    /// Ermöglicht das Zuweisen von Programm-Start, Tastenkombinationen oder Text-Aktionen.
    /// </summary>
    public Dictionary<int, MasterButtonActionConfig> MasterButtonActions { get; set; } = new();

    /// <summary>
    /// MIDI Note-Nummer des Buttons zum Durchschalten der Segment-Display-Modi.
    /// Default: 52 (NAME/VALUE-Button). 0 = deaktiviert.
    /// </summary>
    public int SegmentDisplayCycleButton { get; set; } = 52;

    /// <summary>MQTT Client-Konfiguration.</summary>
    public MqttConfig Mqtt { get; set; } = new();
}
