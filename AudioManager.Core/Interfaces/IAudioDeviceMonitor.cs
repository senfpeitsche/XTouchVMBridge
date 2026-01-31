namespace AudioManager.Core.Interfaces;

/// <summary>
/// Überwacht Audio-Geräte auf Änderungen (z.B. USB-Geräte angeschlossen/entfernt).
/// </summary>
public interface IAudioDeviceMonitor : IDisposable
{
    /// <summary>Wird ausgelöst wenn sich die Geräteliste ändert.</summary>
    event EventHandler? DevicesChanged;

    /// <summary>Aktuelle Anzahl der Audio-Geräte.</summary>
    int DeviceCount { get; }

    /// <summary>Monitoring starten.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Monitoring stoppen.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
