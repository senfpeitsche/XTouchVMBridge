using AudioManager.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace AudioManager.App.Services;

/// <summary>
/// Überwacht Audio-Geräte auf Änderungen (USB an/abstecken).
/// Entspricht AudioDeviceMonitor aus dem Python-Original.
///
/// Läuft als BackgroundService und prüft alle 5 Sekunden die Geräteanzahl.
/// Bei Änderung: Benachrichtigung + Voicemeeter Neustart.
/// Prüft außerdem ob der X-Touch verbunden ist und versucht Reconnect.
/// </summary>
public class AudioDeviceMonitorService : BackgroundService
{
    private readonly ILogger<AudioDeviceMonitorService> _logger;
    private readonly IVoicemeeterService _vm;
    private readonly IMidiDevice _midiDevice;
    private int _previousDeviceCount;
    private bool _changeDetectedLastCheck;

    public event EventHandler? DevicesChanged;

    public AudioDeviceMonitorService(
        ILogger<AudioDeviceMonitorService> logger,
        IVoicemeeterService vm,
        IMidiDevice midiDevice)
    {
        _logger = logger;
        _vm = vm;
        _midiDevice = midiDevice;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _previousDeviceCount = GetDeviceCount();
        _logger.LogInformation("Audio-Device-Monitor gestartet. Geräte: {Count}", _previousDeviceCount);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int currentCount = GetDeviceCount();

                if (currentCount != _previousDeviceCount)
                {
                    _logger.LogInformation(
                        "Audio-Geräteänderung erkannt: {Previous} → {Current}",
                        _previousDeviceCount, currentCount);

                    _previousDeviceCount = currentCount;

                    if (_changeDetectedLastCheck)
                    {
                        // Zweiter Check: tatsächlich handeln
                        _changeDetectedLastCheck = false;
                        DevicesChanged?.Invoke(this, EventArgs.Empty);
                        _vm.Restart();

                        // Nach Änderung länger warten
                        await Task.Delay(10_000, stoppingToken);
                        continue;
                    }

                    _changeDetectedLastCheck = true;
                }
                else
                {
                    _changeDetectedLastCheck = false;
                }

                // X-Touch Reconnect: wenn nicht verbunden, versuche erneut zu verbinden
                if (!_midiDevice.IsConnected)
                {
                    _logger.LogDebug("X-Touch nicht verbunden — versuche Reconnect...");
                    try
                    {
                        await _midiDevice.ConnectAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "X-Touch Reconnect fehlgeschlagen.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler im Audio-Device-Monitor.");
            }

            await Task.Delay(5_000, stoppingToken);
        }
    }

    private static int GetDeviceCount()
    {
        return WaveIn.DeviceCount + WaveOut.DeviceCount;
    }
}
