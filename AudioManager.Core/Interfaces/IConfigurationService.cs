using AudioManager.Core.Models;

namespace AudioManager.Core.Interfaces;

/// <summary>
/// Laden und Speichern der Audio-Manager-Konfiguration (config.json).
/// </summary>
public interface IConfigurationService
{
    /// <summary>Konfiguration laden.</summary>
    AudioManagerConfig Load();

    /// <summary>Konfiguration speichern.</summary>
    void Save(AudioManagerConfig config);

    /// <summary>Standard-Konfiguration erzeugen.</summary>
    AudioManagerConfig CreateDefault();
}
