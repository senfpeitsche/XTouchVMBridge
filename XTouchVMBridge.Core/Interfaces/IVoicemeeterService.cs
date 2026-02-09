using XTouchVMBridge.Core.Models;

namespace XTouchVMBridge.Core.Interfaces;

/// <summary>
/// Abstraktion für den Voicemeeter-Zugriff.
/// Kapselt die native Voicemeeter Remote API.
/// </summary>
public interface IVoicemeeterService : IDisposable
{
    /// <summary>Ob die Verbindung zu Voicemeeter aktiv ist.</summary>
    bool IsConnected { get; }

    /// <summary>Ob sich Parameter seit dem letzten Check geändert haben.</summary>
    bool IsParameterDirty { get; }

    /// <summary>Ob sich Level-Werte seit dem letzten Check geändert haben.</summary>
    bool IsLevelDirty { get; }

    /// <summary>Verbindung zu Voicemeeter herstellen.</summary>
    void Connect();

    /// <summary>Verbindung trennen.</summary>
    void Disconnect();

    /// <summary>Voicemeeter neustarten.</summary>
    void Restart();

    /// <summary>Aktuellen Zustand aller Kanäle lesen.</summary>
    VoicemeeterState GetCurrentState();

    /// <summary>Level eines Kanals in dB lesen (0–15).</summary>
    double GetLevel(int channel);

    /// <summary>Gain eines Kanals setzen (in dB).</summary>
    void SetGain(int channel, double db);

    /// <summary>Mute-Status eines Kanals setzen.</summary>
    void SetMute(int channel, bool muted);

    /// <summary>Solo-Status eines Strips setzen (nur Kanal 0–7).</summary>
    void SetSolo(int channel, bool solo);

    /// <summary>Ob der Kanal ein Input-Strip ist (0–7) oder ein Bus (8–15).</summary>
    bool IsStrip(int channel);

    /// <summary>Generischer Float-Parameter lesen (z.B. "Strip[0].Gain").</summary>
    float GetParameter(string paramName);

    /// <summary>Generischer Float-Parameter setzen (z.B. "Strip[0].Gain", -6.0f).</summary>
    void SetParameter(string paramName, float value);

    /// <summary>Generischen String-Parameter lesen (z.B. "Strip[0].Label").</summary>
    string GetParameterString(string paramName);

    /// <summary>Voicemeeter-Fenster in den Vordergrund bringen (Command.Show = 1).</summary>
    void ShowVoicemeeter();

    /// <summary>Voicemeeter-GUI sperren/entsperren (Command.Lock toggle).</summary>
    void LockGui(bool locked);

    /// <summary>Macro-Button per Index auslösen (0–79). Mode 2 = Trigger.</summary>
    void TriggerMacroButton(int buttonIndex);
}
