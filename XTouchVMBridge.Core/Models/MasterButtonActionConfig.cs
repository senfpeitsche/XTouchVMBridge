namespace XTouchVMBridge.Core.Models;

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
    TriggerMacroButton
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
}
