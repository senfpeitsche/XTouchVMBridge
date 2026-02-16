namespace XTouchVMBridge.Core.Models;

/// <summary>
/// LED-Feedback-Modus nach Ausführung einer Master-Button-Aktion.
/// </summary>
public enum LedFeedbackMode
{
    /// <summary>LED blinkt kurz auf (150ms) als Bestätigung.</summary>
    Blink,

    /// <summary>LED toggelt: 1. Druck → an, 2. Druck → aus.</summary>
    Toggle,

    /// <summary>LED blinkt dauerhaft (Hardware-Blink via Mackie Protocol).</summary>
    Blinking
}

/// <summary>
/// Quelle fuer den LED-Zustand bei Master-Buttons mit VM-Parameter.
/// </summary>
public enum MasterVmLedSource
{
    /// <summary>LED wird ueber LED-Feedback (Blink/Toggle/Blinking) gesteuert.</summary>
    ManualFeedback,

    /// <summary>LED zeigt den aktuellen Zustand des Voicemeeter-Parameters.</summary>
    VoicemeeterState
}
/// <summary>
/// Aktionstypen für Master-Section-Buttons.
/// </summary>
public enum MasterButtonActionType
{
    /// <summary>Keine Aktion zugewiesen.</summary>
    None,

    /// <summary>Voicemeeter Bool-Parameter toggeln.</summary>
    VmParameter,

    /// <summary>Ein Windows-Programm starten.</summary>
    LaunchProgram,

    /// <summary>Eine Tastenkombination senden.</summary>
    SendKeys,

    /// <summary>Einen Text in die Zwischenablage kopieren und einfügen.</summary>
    SendText,

    /// <summary>Durch die Channel-Ansichten (Views) cyclen.</summary>
    CycleChannelView,

    /// <summary>Voicemeeter Audio Engine neu starten.</summary>
    RestartAudioEngine,

    /// <summary>Voicemeeter-Fenster in den Vordergrund bringen.</summary>
    ShowVoicemeeter,

    /// <summary>Voicemeeter-GUI sperren/entsperren (Toggle).</summary>
    LockGui,

    /// <summary>Voicemeeter Macro-Button auslösen (per Index).</summary>
    TriggerMacroButton,
    MqttPublish,
    SelectMqttDevice,
    MqttTransport
}

/// <summary>
/// Konfiguration einer Aktion für einen Master-Section-Button.
/// Wird in config.json unter "masterButtonActions" gespeichert.
/// Key = MIDI Note Number (40–95).
/// </summary>
public class MasterButtonActionConfig
{
    /// <summary>Art der Aktion.</summary>
    public MasterButtonActionType ActionType { get; set; } = MasterButtonActionType.None;

    /// <summary>Voicemeeter-Parametername (für ActionType = VmParameter).</summary>
    public string? VmParameter { get; set; }
    public MasterVmLedSource VmLedSource { get; set; } = MasterVmLedSource.ManualFeedback;

    /// <summary>Pfad zum Programm (für ActionType = LaunchProgram).</summary>
    public string? ProgramPath { get; set; }

    /// <summary>Programmargumente (für ActionType = LaunchProgram).</summary>
    public string? ProgramArgs { get; set; }

    /// <summary>
    /// Tastenkombination (für ActionType = SendKeys).
    /// Format: Modifier+Key, z.B. "Ctrl+Shift+M", "Alt+F4", "F5".
    /// </summary>
    public string? KeyCombination { get; set; }

    /// <summary>Text der gesendet wird (für ActionType = SendText).</summary>
    public string? Text { get; set; }

    /// <summary>Macro-Button Index 0-79 (für ActionType = TriggerMacroButton).</summary>
    public int? MacroButtonIndex { get; set; }

    public string? MqttTopic { get; set; }
    public string? MqttPayloadPressed { get; set; }
    public string? MqttPayloadReleased { get; set; }
    public int MqttQos { get; set; } = 0;
    public bool MqttRetain { get; set; } = false;
    public string? MqttDeviceId { get; set; }
    public string? MqttDeviceCommandTopic { get; set; }
    public string? MqttTransportCommand { get; set; }

    public bool MqttLedEnabled { get; set; } = false;
    public string? MqttLedTopic { get; set; }
    public string? MqttLedPayloadOn { get; set; }
    public string? MqttLedPayloadOff { get; set; }
    public string? MqttLedPayloadBlink { get; set; }
    public string? MqttLedPayloadToggle { get; set; }

    /// <summary>LED-Feedback-Modus: Blink (kurz aufleuchten) oder Toggle (an/aus wechseln).</summary>
    public LedFeedbackMode LedFeedback { get; set; } = LedFeedbackMode.Blink;
}

