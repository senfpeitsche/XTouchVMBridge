using AudioManager.Core.Interfaces;
using AudioManager.Core.Models;
using AudioManager.Voicemeeter.Native;
using Microsoft.Extensions.Logging;

namespace AudioManager.Voicemeeter.Services;

/// <summary>
/// Voicemeeter-Service: Kapselt die VoicemeeterRemote-API.
/// Entspricht XTouchVMinterface.py (VMInterfaceFunctions + VMState).
///
/// Potato-Layout:
///   Strips 0–7: Input-Kanäle
///   Bus 0–7: Output-Busse (logisch als Kanal 8–15)
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
        // DLL-Suchpfad setzen, damit VoicemeeterRemote64.dll gefunden wird
        var dllPath = VoicemeeterRemote.EnsureDllSearchPath();
        if (dllPath != null)
        {
            _logger.LogInformation("Voicemeeter DLL-Pfad gesetzt: {Path}", dllPath);
        }
        else
        {
            _logger.LogWarning("Voicemeeter-Installationsverzeichnis nicht gefunden. " +
                "DLL muss im Systempfad oder Anwendungsverzeichnis liegen.");
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
        if (channel < VoicemeeterState.StripCount)
        {
            // Strip: PostFader Level (type=1), beide Kanäle (L+R), Maximum nehmen
            VoicemeeterRemote.GetLevel(1, channel * 2, out float left);
            VoicemeeterRemote.GetLevel(1, channel * 2 + 1, out float right);
            return Math.Max(left, right);
        }
        else
        {
            // Bus: Output Level (type=3)
            int busIndex = channel - VoicemeeterState.StripCount;
            VoicemeeterRemote.GetLevel(3, busIndex * 8, out float left);
            VoicemeeterRemote.GetLevel(3, busIndex * 8 + 1, out float right);
            return Math.Max(left, right);
        }
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
