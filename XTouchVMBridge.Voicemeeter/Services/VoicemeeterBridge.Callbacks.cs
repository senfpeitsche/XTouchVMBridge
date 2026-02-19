using XTouchVMBridge.Core.Events;
using XTouchVMBridge.Core.Models;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace XTouchVMBridge.Voicemeeter.Services;

/// <summary>
/// Callbacks: X-Touch → Voicemeeter.
/// Verarbeitet Fader, Button, Encoder und Touch-Events vom Hardware-Controller.
/// </summary>
public partial class VoicemeeterBridge
{
    private void OnFaderChanged(object? sender, FaderEventArgs e)
    {
        // Main Fader (channel 8 in Mackie protocol)
        if (e.Channel == 8)
        {
            // Nur verarbeiten wenn Main Fader berührt wird (verhindert MIDI-Feedback-Loop)
            if (!_xtouch.IsMainFaderTouched) return;
            HandleMainFader(e);
            return;
        }

        // Strip faders (channels 0-7)
        if (e.Channel >= CurrentChannelMapping.Length) return;

        // Nur verarbeiten wenn Fader berührt wird (verhindert MIDI-Feedback-Loop)
        if (!_xtouch.Channels[e.Channel].Fader.IsTouched) return;

        int vmCh = CurrentChannelMapping[e.Channel];
        var mapping = GetMapping(vmCh);

        if (mapping?.Fader != null)
        {
            double db = Math.Clamp(e.Db, mapping.Fader.Min, mapping.Fader.Max);
            _vm.SetParameter(mapping.Fader.Parameter, (float)db);
        }
        else
        {
            // Fallback: Standard-Gain
            double db = Math.Max(e.Db, -60.0);
            _vm.SetGain(vmCh, db);
        }

        // dB-Wert temporär anzeigen (wird durch _displayDbUntil geschützt)
        string dbText = e.Db <= -60 ? " -inf " : $"{e.Db:F1}dB";
        _xtouch.SetDisplayText(e.Channel, 1, dbText);

        // Fader-Schutz: Verhindert dass der Sync-Loop den Fader zurücksetzt
        if (e.Channel < _faderProtectUntil.Length)
        {
            _faderProtectUntil[e.Channel] = DateTime.UtcNow + TimeSpan.FromMilliseconds(FaderProtectMs);
        }

        // Display-Schutz für 3 Sekunden nach letzter Fader-Bewegung
        if (e.Channel < _displayDbUntil.Length)
        {
            _displayDbUntil[e.Channel] = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        }
    }

    private void HandleMainFader(FaderEventArgs e)
    {
        var currentView = ChannelViews[_currentViewIndex];
        if (!currentView.MainFaderChannel.HasValue) return;

        int vmCh = currentView.MainFaderChannel.Value;
        var mapping = GetMapping(vmCh);

        if (mapping?.Fader != null)
        {
            double db = Math.Clamp(e.Db, mapping.Fader.Min, mapping.Fader.Max);
            _vm.SetParameter(mapping.Fader.Parameter, (float)db);
        }
        else
        {
            // Fallback: Standard-Gain
            double db = Math.Max(e.Db, -60.0);
            _vm.SetGain(vmCh, db);
        }

        // Fader-Schutz für Main Fader (Index 8)
        _faderProtectUntil[8] = DateTime.UtcNow + TimeSpan.FromMilliseconds(FaderProtectMs);

        _logger.LogDebug("Main Fader -> VM Channel {VmCh}: {Db:F1} dB", vmCh, e.Db);
    }

    private void OnButtonChanged(object? sender, ButtonEventArgs e)
    {
        if (!e.IsPressed) return; // Nur auf Press reagieren

        int vmCh = CurrentChannelMapping[e.Channel];
        var btnMap = GetButtonMapping(vmCh, e.ButtonType);

        if (btnMap != null &&
            btnMap.ActionType == ButtonActionType.VmParameter &&
            !string.IsNullOrWhiteSpace(btnMap.Parameter))
        {
            if (string.Equals(
                    btnMap.Parameter,
                    ButtonMappingConfig.ChannelRecordActionParameter,
                    StringComparison.Ordinal))
            {
                HandleChannelRecordToggle(vmCh);
                _needsFullRefresh = true;
                return;
            }

            // Generisch: Bool-Parameter toggeln
            float current = _vm.GetParameter(btnMap.Parameter);
            _vm.SetParameter(btnMap.Parameter, current > 0.5f ? 0f : 1f);
            _needsFullRefresh = true;
        }
    }

    private void OnEncoderRotated(object? sender, EncoderEventArgs e)
    {
        var encoder = _xtouch.Channels[e.Channel].Encoder;

        // Wenn der Encoder Funktionen hat → Wert der aktiven Funktion ändern
        if (encoder.HasFunctions)
        {
            var fn = encoder.ApplyTicks(e.Ticks);
            if (fn != null)
            {
                // Wert an Voicemeeter senden
                _vm.SetParameter(fn.VmParameter, (float)fn.CurrentValue);

                // Ring-Position am Gerät aktualisieren
                _xtouch.SetEncoderRing(e.Channel, encoder.CalculateCcValue(), encoder.RingMode, encoder.RingLed);

                // Parameter oben, Wert unten anzeigen
                _xtouch.SetDisplayText(e.Channel, 0, fn.Name);
                _xtouch.SetDisplayText(e.Channel, 1, fn.FormatValue());

                // Display-Schutz: Verhindert dass UpdateDisplays() die Anzeige überschreibt
                _displayEncoderUntil[e.Channel] = DateTime.UtcNow + TimeSpan.FromSeconds(3);

                _logger.LogDebug("Encoder {Ch} [{Fn}]: {Val}", e.Channel + 1, fn.Name, fn.FormatValue());

                // Nach 3s wieder auf Kanalnamen zurückschalten
                _scheduler.AddTask(
                    () => RestoreChannelDisplay(e.Channel),
                    TimeSpan.FromSeconds(3),
                    $"encoder_display_{e.Channel}");
            }
        }
    }

    private void OnEncoderPressed(object? sender, EncoderPressEventArgs e)
    {
        if (!e.IsPressed) return;

        var encoder = _xtouch.Channels[e.Channel].Encoder;

        // Wenn der Encoder Funktionen hat → nächste Funktion durchschalten
        if (encoder.HasFunctions)
        {
            var fn = encoder.CycleFunction();
            if (fn != null)
            {
                // Aktuellen Wert aus Voicemeeter lesen
                float currentValue = _vm.GetParameter(fn.VmParameter);
                fn.CurrentValue = currentValue;

                encoder.SyncRingToActiveFunction();
                _xtouch.SetEncoderRing(e.Channel, encoder.CalculateCcValue(), encoder.RingMode, encoder.RingLed);

                // Parameter oben, Wert unten anzeigen
                _xtouch.SetDisplayText(e.Channel, 0, fn.Name);
                _xtouch.SetDisplayText(e.Channel, 1, fn.FormatValue());

                // Display-Schutz: Verhindert dass UpdateDisplays() die Anzeige überschreibt
                _displayEncoderUntil[e.Channel] = DateTime.UtcNow + TimeSpan.FromSeconds(3);

                _logger.LogInformation("Encoder {Ch}: Funktion → {Fn} ({Val})",
                    e.Channel + 1, fn.Name, fn.FormatValue());

                // Nach 3s wieder auf Kanalnamen zurückschalten
                _scheduler.AddTask(
                    () => RestoreChannelDisplay(e.Channel),
                    TimeSpan.FromSeconds(3),
                    $"encoder_display_{e.Channel}");
            }
        }
    }

    /// <summary>
    /// Stellt das normale Kanal-Display wieder her (Name oben, View-Name unten).
    /// </summary>
    private void RestoreChannelDisplay(int xtCh)
    {
        int vmCh = CurrentChannelMapping[xtCh];
        string vmLabel = GetVmLabel(vmCh);
        _xtouch.SetDisplayText(xtCh, 0, vmLabel);
        _xtouch.SetDisplayText(xtCh, 1, ChannelViews[_currentViewIndex].Name);
    }

    private void HandleChannelRecordToggle(int vmChannel)
    {
        try
        {
            if (_isRecorderActive)
            {
                _vm.SetParameter("Recorder.Stop", 1f);
                _isRecorderActive = false;
                _logger.LogInformation("Recorder gestoppt (Channel {Channel}).", vmChannel + 1);
                return;
            }

            ArmRecorderChannel(vmChannel);

            string filePath = BuildRecordingFilePath(vmChannel);
            _vm.SetParameterString("Recorder.FileName", filePath);
            _vm.SetParameter("Recorder.Record", 1f);
            _isRecorderActive = true;

            _logger.LogInformation("Recorder gestartet: Channel {Channel}, Datei: {FilePath}", vmChannel + 1, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Umschalten der Aufnahme fuer Channel {Channel}.", vmChannel + 1);
        }
    }

    private void ArmRecorderChannel(int vmChannel)
    {
        for (int i = 0; i < 8; i++)
        {
            _vm.SetParameter($"Recorder.ArmStrip[{i}]", 0f);
            _vm.SetParameter($"Recorder.ArmBus[{i}]", 0f);
        }

        if (vmChannel < 8)
        {
            _vm.SetParameter($"Recorder.ArmStrip[{vmChannel}]", 1f);
        }
        else
        {
            _vm.SetParameter($"Recorder.ArmBus[{vmChannel - 8}]", 1f);
        }
    }

    private string BuildRecordingFilePath(int vmChannel)
    {
        string channelName = _config.Channels.TryGetValue(vmChannel, out var channelConfig)
            ? channelConfig.Name
            : $"CH{vmChannel + 1}";

        string safeChannelName = SanitizeFileNamePart(channelName);
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string fileName = $"{safeChannelName}_{timestamp}.wav";

        string recordingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XTouchVMBridge",
            "Recordings");

        Directory.CreateDirectory(recordingsDir);
        return Path.Combine(recordingsDir, fileName);
    }

    private static string SanitizeFileNamePart(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "Channel";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(input
            .Trim()
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "Channel" : sanitized;
    }

    private void OnFaderTouched(object? sender, FaderTouchEventArgs e)
    {

        if (e.IsTouched)
        {
            var now = DateTime.Now;
            int channel = e.Channel; // 0-7 für Channel, 8 für Main

            // Doppel-Touch-Erkennung: Fader auf 0 dB setzen
            if (channel < _lastFaderTouchTime.Length)
            {
                var timeSinceLastTouch = (now - _lastFaderTouchTime[channel]).TotalMilliseconds;

                if (timeSinceLastTouch < DoubleTapThresholdMs && timeSinceLastTouch > 50) // Min 50ms um Prellen zu vermeiden
                {
                    _logger.LogDebug("Doppel-Touch auf Fader {Channel} erkannt (Zeit: {Ms}ms)", channel, timeSinceLastTouch);
                    SetFaderTo0dB(channel);
                    _lastFaderTouchTime[channel] = DateTime.MinValue; // Reset
                    return;
                }

                _lastFaderTouchTime[channel] = now;
            }

            // Bei Touch: aktuellen dB-Wert anzeigen (nur für Channel-Fader 0-7)
            if (e.Channel >= CurrentChannelMapping.Length) return;

            int vmCh = CurrentChannelMapping[e.Channel];
            var mapping = GetMapping(vmCh);

            double db;
            if (mapping?.Fader != null)
            {
                db = _vm.GetParameter(mapping.Fader.Parameter);
            }
            else
            {
                db = _vmState.Gains[vmCh];
            }

            string dbText = db <= -60 ? " -inf " : $"{db:F1}dB";
            _xtouch.SetDisplayText(e.Channel, 1, dbText);
        }
        else
        {
            // Bei Release: Sofort den aktuellen Wert aus Voicemeeter an X-Touch senden
            // Das hält den Motor-Fader auf der korrekten Position
            if (e.Channel < 8 && e.Channel < CurrentChannelMapping.Length)
            {
                int vmCh = CurrentChannelMapping[e.Channel];
                var mapping = GetMapping(vmCh);

                double currentDb;
                if (mapping?.Fader != null)
                {
                    currentDb = _vm.GetParameter(mapping.Fader.Parameter);
                }
                else
                {
                    string prefix = vmCh < 8 ? $"Strip[{vmCh}]" : $"Bus[{vmCh - 8}]";
                    currentDb = _vm.GetParameter($"{prefix}.Gain");
                }

                _xtouch.SetFaderDb(e.Channel, currentDb);
            }
            else if (e.Channel == 8)
            {
                // Main Fader Release
                var currentView = ChannelViews[_currentViewIndex];
                if (currentView.MainFaderChannel.HasValue)
                {
                    int vmCh = currentView.MainFaderChannel.Value;
                    var mapping = GetMapping(vmCh);

                    double currentDb;
                    if (mapping?.Fader != null)
                    {
                        currentDb = _vm.GetParameter(mapping.Fader.Parameter);
                    }
                    else
                    {
                        string prefix = vmCh < 8 ? $"Strip[{vmCh}]" : $"Bus[{vmCh - 8}]";
                        currentDb = _vm.GetParameter($"{prefix}.Gain");
                    }

                    _xtouch.SetFaderDb(8, currentDb);
                }
            }

            // dB-Wert noch 3 Sekunden anzeigen lassen (nur für Channel-Fader 0-7)
            if (e.Channel < _displayDbUntil.Length)
            {
                _displayDbUntil[e.Channel] = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            }
        }
    }

    /// <summary>
    /// Setzt einen Fader auf 0 dB.
    /// </summary>
    /// <param name="channel">0-7 für Channel-Fader, 8 für Main Fader</param>
    private void SetFaderTo0dB(int channel)
    {
        int vmCh;
        ControlMappingConfig? mapping;

        if (channel == 8)
        {
            // Main Fader
            var currentView = ChannelViews[_currentViewIndex];
            if (!currentView.MainFaderChannel.HasValue) return;
            vmCh = currentView.MainFaderChannel.Value;
            mapping = GetMapping(vmCh);
        }
        else
        {
            // Channel Fader 0-7
            if (channel >= CurrentChannelMapping.Length) return;
            vmCh = CurrentChannelMapping[channel];
            mapping = GetMapping(vmCh);
        }

        if (mapping?.Fader != null)
        {
            // 0 dB setzen (innerhalb der konfigurierten Grenzen)
            double db = Math.Clamp(0.0, mapping.Fader.Min, mapping.Fader.Max);
            _vm.SetParameter(mapping.Fader.Parameter, (float)db);
        }
        else
        {
            // Fallback: Standard-Gain auf 0 dB
            _vm.SetGain(vmCh, 0.0);
        }

        // Fader-Position auf X-Touch aktualisieren
        _xtouch.SetFaderDb(channel, 0.0);

        // Fader-Schutz setzen
        if (channel < _faderProtectUntil.Length)
        {
            _faderProtectUntil[channel] = DateTime.UtcNow + TimeSpan.FromMilliseconds(FaderProtectMs);
        }

        // Display aktualisieren (nur für Channel-Fader 0-7)
        if (channel < _displayDbUntil.Length)
        {
            _xtouch.SetDisplayText(channel, 1, " 0.0dB");
            _displayDbUntil[channel] = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        }

        _logger.LogDebug("Fader {Channel} auf 0 dB gesetzt (Doppel-Touch)", channel);
    }
}
