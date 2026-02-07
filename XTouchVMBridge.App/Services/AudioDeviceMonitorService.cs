using XTouchVMBridge.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace XTouchVMBridge.App.Services;

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
    private int _reconnectAttempts;

    private const int BasePollingMs = 3_000;
    private const int MaxReconnectDelayMs = 30_000;
    private const int MaxReconnectAttempts = 100;

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

                // Aktive Prüfung: Gerät noch vorhanden obwohl IsConnected true?
                if (_midiDevice.IsConnected && !_midiDevice.IsDeviceStillPresent())
                {
                    _logger.LogWarning("X-Touch ist nicht mehr in der Geräteliste — erzwinge Disconnect.");
                    _midiDevice.Disconnect();
                }

                // X-Touch Reconnect mit Exponential Backoff
                if (!_midiDevice.IsConnected)
                {
                    if (_reconnectAttempts < MaxReconnectAttempts)
                    {
                        // Exponential Backoff: 1s, 2s, 4s, 8s, ... bis MaxReconnectDelayMs
                        int backoffMs = Math.Min(1_000 * (1 << Math.Min(_reconnectAttempts, 5)), MaxReconnectDelayMs);
                        _reconnectAttempts++;

                        _logger.LogDebug("X-Touch nicht verbunden — Reconnect-Versuch {Attempt} (Wartezeit: {Delay}ms)...",
                            _reconnectAttempts, backoffMs);

                        await Task.Delay(backoffMs, stoppingToken);

                        try
                        {
                            await _midiDevice.ConnectAsync(stoppingToken);

                            if (_midiDevice.IsConnected)
                            {
                                _logger.LogInformation("X-Touch erfolgreich wiederverbunden nach {Attempts} Versuch(en).", _reconnectAttempts);
                                _reconnectAttempts = 0;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "X-Touch Reconnect fehlgeschlagen (Versuch {Attempt}).", _reconnectAttempts);
                        }
                    }
                    else if (_reconnectAttempts == MaxReconnectAttempts)
                    {
                        _reconnectAttempts++;
                        _logger.LogWarning("X-Touch Reconnect: Maximale Versuche ({Max}) erreicht. " +
                            "Prüfe weiterhin alle {Interval}s.", MaxReconnectAttempts, MaxReconnectDelayMs / 1000);
                    }
                    else
                    {
                        // Nach Max-Versuchen: weiterhin periodisch prüfen, aber seltener
                        await Task.Delay(MaxReconnectDelayMs, stoppingToken);
                        try
                        {
                            await _midiDevice.ConnectAsync(stoppingToken);
                            if (_midiDevice.IsConnected)
                            {
                                _logger.LogInformation("X-Touch erfolgreich wiederverbunden (nach erweiterter Wartezeit).");
                                _reconnectAttempts = 0;
                            }
                        }
                        catch { /* still trying */ }
                    }
                }
                else
                {
                    // Verbindung steht — Reconnect-Zähler zurücksetzen
                    _reconnectAttempts = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler im Audio-Device-Monitor.");
            }

            await Task.Delay(BasePollingMs, stoppingToken);
        }
    }

    private static int GetDeviceCount()
    {
        return WaveIn.DeviceCount + WaveOut.DeviceCount;
    }
}
