namespace AudioManager.Core.Models;

/// <summary>
/// Gesamtkonfiguration der Anwendung.
/// Wird via IOptions{T} per Dependency Injection bereitgestellt.
/// </summary>
public class AudioManagerConfig
{
    public const string SectionName = "AudioManager";

    /// <summary>Kanal-Konfigurationen (Key = Kanal-Index 0–15).</summary>
    public Dictionary<int, ChannelConfig> Channels { get; set; } = new();

    /// <summary>Voicemeeter API-Typ ("potato", "banana", "basic").</summary>
    public string VoicemeeterApiType { get; set; } = "potato";

    /// <summary>Intervall in ms für das Device-Monitoring.</summary>
    public int DeviceMonitorIntervalMs { get; set; } = 5000;

    /// <summary>Ob die XTouch-Integration aktiviert ist.</summary>
    public bool EnableXTouch { get; set; } = true;

    /// <summary>Ob die Fantom-MIDI-Integration aktiviert ist.</summary>
    public bool EnableFantom { get; set; } = true;

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
}
