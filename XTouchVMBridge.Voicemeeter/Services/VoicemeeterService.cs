using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Core.Models;
using XTouchVMBridge.Voicemeeter.Native;
using Microsoft.Extensions.Logging;

namespace XTouchVMBridge.Voicemeeter.Services;

/// <summary>
/// Thin wrapper around the native Voicemeeter Remote API.
/// Handles connection lifecycle, parameter IO, command helpers, and state snapshots.
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


    public void Connect()
    {
        // Fallback: ensure DLL path can be resolved even if startup path setup was skipped.
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


    public bool IsParameterDirty => VoicemeeterRemote.IsParametersDirty() == 1;

    public bool IsLevelDirty
    {
        get
        {
            return true; // Level values are always read by polling.
        }
    }


    public double GetLevel(int channel)
    {
        float linear;
        if (channel < VoicemeeterState.StripCount)
        {
            int leftIndex;
            int rightIndex;
            if (channel <= 4)
            {
                leftIndex = channel * 2;
                rightIndex = leftIndex + 1;
            }
            else
            {
                int virtualStripOffset = 10; // 5 * 2 slots occupied by physical strips
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
            int busIndex = channel - VoicemeeterState.StripCount;
            VoicemeeterRemote.GetLevel(3, busIndex * 8, out float left);
            VoicemeeterRemote.GetLevel(3, busIndex * 8 + 1, out float right);
            linear = Math.Max(left, right);
        }

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

        int length = Array.IndexOf(buffer, (byte)0);
        if (length < 0) length = buffer.Length;
        return System.Text.Encoding.ASCII.GetString(buffer, 0, length);
    }


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

        VoicemeeterRemote.MacroButtonSetStatus(buttonIndex, 1.0f, 2);
        _logger.LogInformation("Macro-Button {Index} ausgelöst.", buttonIndex);
    }


    public VoicemeeterState GetCurrentState()
    {
        var state = new VoicemeeterState();

        for (int i = 0; i < VoicemeeterState.TotalChannels; i++)
        {
            string prefix = IsStrip(i)
                ? $"Strip[{i}]"
                : $"Bus[{i - VoicemeeterState.StripCount}]";

            VoicemeeterRemote.GetParameterFloat($"{prefix}.Mute", out float mute);
            state.Mutes[i] = mute > 0.5f;

            VoicemeeterRemote.GetParameterFloat($"{prefix}.Gain", out float gain);
            state.Gains[i] = gain;

            if (IsStrip(i) && i < VoicemeeterState.StripCount)
            {
                VoicemeeterRemote.GetParameterFloat($"Strip[{i}].Solo", out float solo);
                state.Solos[i] = solo > 0.5f;
            }

            state.Levels[i] = GetLevel(i);
        }

        return state;
    }


    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
        GC.SuppressFinalize(this);
    }
}

