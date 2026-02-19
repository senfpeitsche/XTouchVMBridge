using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Core.Models;
using XTouchVMBridge.Voicemeeter.Native;
using Microsoft.Extensions.Logging;

namespace XTouchVMBridge.Voicemeeter.Services;

/// <summary>
/// Voicemeeter-Service: Kapselt die VoicemeeterRemote-API.
/// Entspricht XTouchVMinterface.py (VMInterfaceFunctions + VMState).
///
/// Potato-Layout:
///   Strips 0–7: Input-Kanäle
///   Bus 0–7: Output-Busse (logisch als Kanal 8–15)
///
/// Hinweis: Der DLL-Suchpfad für VoicemeeterRemote64.dll wird in
/// <c>App.OnStartup</c> gesetzt, bevor die HostedServices starten.
/// <see cref="Connect"/> ruft <see cref="VoicemeeterRemote.EnsureDllSearchPath"/>
/// nur noch als Fallback auf.
/// </summary>
public class VoicemeeterService : IVoicemeeterService
{
    private readonly ILogger<VoicemeeterService> _logger;
    private bool _isConnected;
    private bool _disposed;

    public bool IsConnected => _isConnected;

    public VoicemeeterService(ILogger<VoicemeeterService> logger)
    {
        _logger = logger;
    }

    // ─── Verbindung ─────────────────────────────────────────────────

    public void Connect()
    {
        // DLL-Suchpfad wird bereits in App.OnStartup gesetzt (vor Host-Start).
        // Dieser Aufruf dient als Fallback, falls Connect() ohne App-Kontext aufgerufen wird.
        var dllPath = VoicemeeterRemote.EnsureDllSearchPath();
        if (dllPath != null)
        {
            _logger.LogInformation("Voicemeeter DLL-Pfad gesetzt: {Path}", dllPath);
        }

        int result = VoicemeeterRemote.Login();
        _isConnected = result >= 0;

        if (_isConnected)
        {
            VoicemeeterRemote.GetVoicemeeterType(out int type);
            string typeName = type switch { 1 => "Basic", 2 => "Banana", 3 => "Potato", _ => $"Unknown({type})" };
            _logger.LogInformation("Voicemeeter verbunden: {Type}", typeName);
        }
        else
        {
            _logger.LogError("Voicemeeter Verbindung fehlgeschlagen (Code: {Result}).", result);
        }
    }

    public void Disconnect()
    {
        if (_isConnected)
        {
            VoicemeeterRemote.Logout();
            _isConnected = false;
            _logger.LogInformation("Voicemeeter getrennt.");
        }
    }

    public void Restart()
    {
        VoicemeeterRemote.SetParameterFloat("Command.Restart", 1.0f);
        _logger.LogInformation("Voicemeeter Neustart angefordert.");
    }

    // ─── Dirty Flags ────────────────────────────────────────────────

    public bool IsParameterDirty => VoicemeeterRemote.IsParametersDirty() == 1;

    public bool IsLevelDirty
    {
        get
        {
            // Voicemeeter hat kein separates LevelDirty-Flag — wir pollen die Levels direkt.
            // In der Python-Version wird vm.ldirty geprüft, was intern dasselbe macht.
            return true; // Levels werden im Polling-Intervall immer abgefragt
        }
    }

    // ─── Parameter lesen/schreiben ──────────────────────────────────

    public double GetLevel(int channel)
    {
        float linear;
        if (channel < VoicemeeterState.StripCount)
        {
            // Strip: PostFader Level (type=1), beide Kanaele (L+R), Maximum nehmen.
            // Potato-Layout:
            // - Strip 0..4: Stereo (je 2 Level-Slots)
            // - Strip 5..7: Virtual 8ch (je 8 Level-Slots)
            // Daher ist "strip * 2" fuer 5..7 falsch und liefert dort oft 0.
            int leftIndex;
            int rightIndex;
            if (channel <= 4)
            {
                leftIndex = channel * 2;
                rightIndex = leftIndex + 1;
            }
            else
            {
                int virtualStripOffset = 10; // 5 * 2 Slots der physischen Strips
                int virtualStripIndex = channel - 5;
                leftIndex = virtualStripOffset + (virtualStripIndex * 8);
                rightIndex = leftIndex + 1;
            }

            VoicemeeterRemote.GetLevel(1, leftIndex, out float left);
            VoicemeeterRemote.GetLevel(1, rightIndex, out float right);
            linear = Math.Max(left, right);
        }
        else
        {
            // Bus: Output Level (type=3)
            int busIndex = channel - VoicemeeterState.StripCount;
            VoicemeeterRemote.GetLevel(3, busIndex * 8, out float left);
            VoicemeeterRemote.GetLevel(3, busIndex * 8 + 1, out float right);
            linear = Math.Max(left, right);
        }

        // VBVMR_GetLevel gibt lineare Amplitude zurueck (0.0-1.0+), nicht dB.
        // Umrechnung: dB = 20 * log10(linear). Bei Stille (0.0) -> -200 dB.
        return linear > 0 ? 20.0 * Math.Log10(linear) : -200.0;
    }

    public void SetGain(int channel, double db)
    {
        string param = IsStrip(channel)
            ? $"Strip[{channel}].Gain"
            : $"Bus[{channel - VoicemeeterState.StripCount}].Gain";

        VoicemeeterRemote.SetParameterFloat(param, (float)db);
    }

    public void SetMute(int channel, bool muted)
    {
        string param = IsStrip(channel)
            ? $"Strip[{channel}].Mute"
            : $"Bus[{channel - VoicemeeterState.StripCount}].Mute";

        VoicemeeterRemote.SetParameterFloat(param, muted ? 1.0f : 0.0f);
    }

    public void SetSolo(int channel, bool solo)
    {
        if (!IsStrip(channel))
        {
            _logger.LogWarning("Solo nur für Strips (0–7), nicht für Bus {Channel}.", channel);
            return;
        }

        VoicemeeterRemote.SetParameterFloat($"Strip[{channel}].Solo", solo ? 1.0f : 0.0f);
    }

    public bool IsStrip(int channel) => channel < VoicemeeterState.StripCount;

    // ─── Generische Parameter ─────────────────────────────────────────

    public float GetParameter(string paramName)
    {
        VoicemeeterRemote.GetParameterFloat(paramName, out float value);
        return value;
    }

    public void SetParameter(string paramName, float value)
    {
        VoicemeeterRemote.SetParameterFloat(paramName, value);
    }

    public void SetParameterString(string paramName, string value)
    {
        VoicemeeterRemote.SetParameterStringA(paramName, value);
    }

    public string GetParameterString(string paramName)
    {
        var buffer = new byte[512];
        int result = VoicemeeterRemote.GetParameterStringA(paramName, buffer);
        if (result != 0) return "";

        // Null-terminated ANSI-String aus dem Buffer lesen
        int length = Array.IndexOf(buffer, (byte)0);
        if (length < 0) length = buffer.Length;
        return System.Text.Encoding.ASCII.GetString(buffer, 0, length);
    }

    // ─── Commands ─────────────────────────────────────────────────────

    public void ShowVoicemeeter()
    {
        VoicemeeterRemote.SetParameterFloat("Command.Show", 1.0f);
        _logger.LogInformation("Voicemeeter-Fenster in den Vordergrund gebracht.");
    }

    public void LockGui(bool locked)
    {
        VoicemeeterRemote.SetParameterFloat("Command.Lock", locked ? 1.0f : 0.0f);
        _logger.LogInformation("Voicemeeter GUI {State}.", locked ? "gesperrt" : "entsperrt");
    }

    public void TriggerMacroButton(int buttonIndex)
    {
        if (buttonIndex < 0 || buttonIndex > 79)
        {
            _logger.LogWarning("TriggerMacroButton: Index {Index} außerhalb des gültigen Bereichs (0–79).", buttonIndex);
            return;
        }

        // Mode 2 = Trigger (kurzer Impuls)
        VoicemeeterRemote.MacroButtonSetStatus(buttonIndex, 1.0f, 2);
        _logger.LogInformation("Macro-Button {Index} ausgelöst.", buttonIndex);
    }

    // ─── State Snapshot ─────────────────────────────────────────────

    public VoicemeeterState GetCurrentState()
    {
        var state = new VoicemeeterState();

        for (int i = 0; i < VoicemeeterState.TotalChannels; i++)
        {
            string prefix = IsStrip(i)
                ? $"Strip[{i}]"
                : $"Bus[{i - VoicemeeterState.StripCount}]";

            // Mute
            VoicemeeterRemote.GetParameterFloat($"{prefix}.Mute", out float mute);
            state.Mutes[i] = mute > 0.5f;

            // Gain
            VoicemeeterRemote.GetParameterFloat($"{prefix}.Gain", out float gain);
            state.Gains[i] = gain;

            // Solo (nur Strips)
            if (IsStrip(i) && i < VoicemeeterState.StripCount)
            {
                VoicemeeterRemote.GetParameterFloat($"Strip[{i}].Solo", out float solo);
                state.Solos[i] = solo > 0.5f;
            }

            // Level
            state.Levels[i] = GetLevel(i);
        }

        return state;
    }

    // ─── IDisposable ────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
        GC.SuppressFinalize(this);
    }
}

