namespace XTouchVMBridge.Core.Models;

/// <summary>
/// Konfiguration aller Control-Zuweisungen eines VM-Kanals (0–15).
/// Wird in config.json unter "mappings" gespeichert.
/// Jeder VM-Kanal hat ein eigenes Mapping für Fader, Buttons und Encoder.
/// </summary>
public class ControlMappingConfig
{
    /// <summary>Fader-Zuweisung (Float-Parameter mit Min/Max/Step).</summary>
    public FaderMappingConfig? Fader { get; set; }

    /// <summary>
    /// Button-Zuweisungen (Key = ButtonType-Name: "Mute", "Solo", "Rec", "Select").
    /// Wert null = nicht zugewiesen.
    /// </summary>
    public Dictionary<string, ButtonMappingConfig?> Buttons { get; set; } = new();

    /// <summary>
    /// Encoder-Funktionsliste (zyklisch durchschaltbar per Encoder-Press).
    /// Leere Liste = Encoder nicht zugewiesen.
    /// </summary>
    public List<EncoderFunctionConfig> EncoderFunctions { get; set; } = new();
}

/// <summary>
/// Fader-Zuweisung: ein Float-Parameter mit Wertebereich.
/// </summary>
public class FaderMappingConfig
{
    /// <summary>Voicemeeter-Parametername (z.B. "Strip[0].Gain").</summary>
    public string Parameter { get; set; } = "";

    /// <summary>Minimaler Wert in der Parametereinheit.</summary>
    public double Min { get; set; } = -60;

    /// <summary>Maximaler Wert in der Parametereinheit.</summary>
    public double Max { get; set; } = 12;

    /// <summary>Schrittweite (für Display-Rundung).</summary>
    public double Step { get; set; } = 0.1;
}

/// <summary>
/// Button-Zuweisung: ein Bool-Parameter (Toggle).
/// </summary>
public class ButtonMappingConfig
{
    /// <summary>
    /// Spezialwert fuer <see cref="Parameter"/>:
    /// Startet/stoppt die Voicemeeter-Aufnahme und setzt den Dateinamen
    /// auf "Kanalname_yyyy-MM-dd_HH-mm-ss.wav".
    /// </summary>
    public const string ChannelRecordActionParameter = "__xtvm_record_channel__";

    /// <summary>Voicemeeter-Parametername (z.B. "Strip[0].Mute").</summary>
    public string Parameter { get; set; } = "";

    /// <summary>Aktionstyp des Buttons (VM-Parameter oder MQTT Publish).</summary>
    public ButtonActionType ActionType { get; set; } = ButtonActionType.VmParameter;

    /// <summary>MQTT Publish-Konfiguration (bei ActionType=MqttPublish).</summary>
    public MqttButtonPublishConfig? MqttPublish { get; set; }

    /// <summary>MQTT Empfangs-Konfiguration fuer LED-Steuerung.</summary>
    public MqttButtonLedReceiveConfig? MqttLedReceive { get; set; }
}

public enum ButtonActionType
{
    VmParameter = 0,
    MqttPublish = 1
}

public class MqttButtonPublishConfig
{
    public string Topic { get; set; } = "";
    public string PayloadPressed { get; set; } = "on";
    public string PayloadReleased { get; set; } = "";
    public int Qos { get; set; } = 0;
    public bool Retain { get; set; } = false;
}

public class MqttButtonLedReceiveConfig
{
    public bool Enabled { get; set; } = false;
    public string Topic { get; set; } = "";
    public string PayloadOn { get; set; } = "on";
    public string PayloadOff { get; set; } = "off";
    public string PayloadBlink { get; set; } = "blink";
    public string PayloadToggle { get; set; } = "toggle";
    public bool IgnoreCase { get; set; } = true;
}

/// <summary>
/// Encoder-Funktions-Konfiguration: ein Float-Parameter mit Label und Wertebereich.
/// Entspricht den Daten einer <see cref="XTouchVMBridge.Core.Hardware.EncoderFunction"/>.
/// </summary>
public class EncoderFunctionConfig
{
    /// <summary>Anzeigename (max 7 Zeichen, passend für X-Touch Display).</summary>
    public string Label { get; set; } = "";

    /// <summary>Voicemeeter-Parametername (z.B. "Strip[0].EQGain3").</summary>
    public string Parameter { get; set; } = "";

    /// <summary>Minimaler Wert.</summary>
    public double Min { get; set; }

    /// <summary>Maximaler Wert.</summary>
    public double Max { get; set; }

    /// <summary>Schrittweite pro Encoder-Tick.</summary>
    public double Step { get; set; } = 0.5;

    /// <summary>Einheit für die Anzeige (z.B. "dB", "%", "").</summary>
    public string Unit { get; set; } = "dB";
}
