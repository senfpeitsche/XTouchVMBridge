using XTouchVMBridge.Core.Models;

namespace XTouchVMBridge.Core.Interfaces;

/// <summary>
/// Laden und Speichern der Audio-Manager-Konfiguration (config.json).
/// </summary>
public interface IConfigurationService
{
    /// <summary>Konfiguration laden.</summary>
    XTouchVMBridgeConfig Load();

    /// <summary>Konfiguration speichern.</summary>
    void Save(XTouchVMBridgeConfig config);

    /// <summary>Standard-Konfiguration erzeugen.</summary>
    XTouchVMBridgeConfig CreateDefault();
}
