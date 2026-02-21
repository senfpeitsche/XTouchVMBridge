using XTouchVMBridge.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace XTouchVMBridge.App.Services;

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
                        _changeDetectedLastCheck = false;
                        DevicesChanged?.Invoke(this, EventArgs.Empty);
                        _vm.Restart();

                        await Task.Delay(10_000, stoppingToken);
                        continue;
                    }

                    _changeDetectedLastCheck = true;
                }
                else
                {
                    _changeDetectedLastCheck = false;
                }

                if (_midiDevice.IsConnected && !_midiDevice.IsDeviceStillPresent())
                {
                    _logger.LogWarning("X-Touch ist nicht mehr in der Geräteliste — erzwinge Disconnect.");
                    _midiDevice.Disconnect();
                }

                if (!_midiDevice.IsConnected)
                {
                    if (_reconnectAttempts < MaxReconnectAttempts)
                    {
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
