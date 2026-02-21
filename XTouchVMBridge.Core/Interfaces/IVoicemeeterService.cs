using XTouchVMBridge.Core.Models;

namespace XTouchVMBridge.Core.Interfaces;

public interface IVoicemeeterService : IDisposable
{
    bool IsConnected { get; }

    bool IsParameterDirty { get; }

    bool IsLevelDirty { get; }

    void Connect();

    void Disconnect();

    void Restart();

    VoicemeeterState GetCurrentState();

    double GetLevel(int channel);

    void SetGain(int channel, double db);

    void SetMute(int channel, bool muted);

    void SetSolo(int channel, bool solo);

    bool IsStrip(int channel);

    float GetParameter(string paramName);

    void SetParameter(string paramName, float value);

    string GetParameterString(string paramName);

    void SetParameterString(string paramName, string value);

    void ShowVoicemeeter();

    void LockGui(bool locked);

    void TriggerMacroButton(int buttonIndex);
}
