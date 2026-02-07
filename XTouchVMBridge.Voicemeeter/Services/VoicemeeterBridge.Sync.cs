using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Hardware;

namespace XTouchVMBridge.Voicemeeter.Services;

/// <summary>
/// Sync-Methoden: Level-Meter, Parameter-Updates, Fader-Sync, Display-Updates, Encoder-Ring.
/// Wird im 100ms Polling-Loop von ExecuteAsync aufgerufen.
/// </summary>
public partial class VoicemeeterBridge
{
    private void UpdateLevels()
    {
        for (int xtCh = 0; xtCh < MidiDevice_ChannelCount(); xtCh++)
        {
            int vmCh = CurrentChannelMapping[xtCh];
            double level = _vm.GetLevel(vmCh);

            // Nur bei Änderung aktualisieren
            if (Math.Abs(level - _levelCache[vmCh]) > 0.1)
            {
                _levelCache[vmCh] = level;
                int meterLevel = LevelMeterControl.DbToLevel(level);
                _xtouch.SetLevelMeter(xtCh, meterLevel);
            }
        }
    }

    private void UpdateParameters()
    {
        _vmState = _vm.GetCurrentState();

        var now = DateTime.UtcNow;

        // Prüfen ob IRGENDEIN Fader berührt oder geschützt ist
        bool anyFaderActive = false;
        for (int i = 0; i < MidiDevice_ChannelCount(); i++)
        {
            if (_xtouch.Channels[i].Fader.IsTouched ||
                (i < _faderProtectUntil.Length && _faderProtectUntil[i] > now))
            {
                anyFaderActive = true;
                break;
            }
        }
        // Auch Main Fader prüfen
        if (_xtouch.IsMainFaderTouched || _faderProtectUntil[8] > now)
            anyFaderActive = true;

        for (int xtCh = 0; xtCh < MidiDevice_ChannelCount(); xtCh++)
        {
            int vmCh = CurrentChannelMapping[xtCh];
            var mapping = GetMapping(vmCh);

            // Fader-Position synchronisieren - aber NUR wenn KEIN Fader aktiv ist
            // Das verhindert MIDI-Kollisionen die den Fader zurücksetzen
            double dbToSet;
            if (mapping?.Fader != null)
            {
                dbToSet = _vm.GetParameter(mapping.Fader.Parameter);
            }
            else
            {
                // Fallback: Gain direkt aus Voicemeeter lesen (nicht aus gecachtem State!)
                string prefix = vmCh < 8 ? $"Strip[{vmCh}]" : $"Bus[{vmCh - 8}]";
                dbToSet = _vm.GetParameter($"{prefix}.Gain");
            }

            if (!anyFaderActive)
            {
                _xtouch.SetFaderDb(xtCh, dbToSet);
            }

            // Gain-Änderung aus Voicemeeter erkennen (z.B. per GUI) → dB im Display anzeigen
            if (_gainCacheInitialized && Math.Abs(dbToSet - _lastGainValues[xtCh]) > 0.05)
            {
                // Nur anzeigen wenn der Fader NICHT vom X-Touch bewegt wird
                bool isTouched = _xtouch.Channels[xtCh].Fader.IsTouched;
                if (!isTouched)
                {
                    string dbText = dbToSet <= -60 ? " -inf " : $"{dbToSet:F1}dB";
                    _xtouch.SetDisplayText(xtCh, 1, dbText);
                    _displayDbUntil[xtCh] = DateTime.UtcNow + TimeSpan.FromSeconds(3);
                }
            }
            _lastGainValues[xtCh] = dbToSet;

            // Button-LEDs synchronisieren (alle 4 Buttons)
            foreach (XTouchButtonType btnType in Enum.GetValues<XTouchButtonType>())
            {
                var btnMap = GetButtonMapping(vmCh, btnType);
                if (btnMap != null)
                {
                    float val = _vm.GetParameter(btnMap.Parameter);
                    _xtouch.SetButtonLed(xtCh, btnType,
                        val > 0.5f ? LedState.On : LedState.Off);
                }
                // Kein Mapping → LED-State nicht überschreiben (Panel-Toggle beibehalten)
            }

            // Encoder-Ring synchronisieren (nur wenn kein Encoder-Schutz aktiv)
            if (!(xtCh < _displayEncoderUntil.Length && _displayEncoderUntil[xtCh] > DateTime.UtcNow))
                SyncEncoderRing(xtCh, vmCh);
        }

        // Sync Main Fader (channel 8)
        SyncMainFader(anyFaderActive);

        // Nach dem ersten Durchlauf ist der Gain-Cache initialisiert
        if (!_gainCacheInitialized)
            _gainCacheInitialized = true;

        UpdateDisplays();
    }

    private void SyncMainFader(bool anyFaderActive)
    {
        var currentView = ChannelViews[_currentViewIndex];
        if (!currentView.MainFaderChannel.HasValue) return;

        int vmCh = currentView.MainFaderChannel.Value;
        var mapping = GetMapping(vmCh);

        double db;
        if (mapping?.Fader != null)
        {
            db = _vm.GetParameter(mapping.Fader.Parameter);
        }
        else
        {
            // Fallback: Gain direkt aus Voicemeeter lesen
            string prefix = vmCh < 8 ? $"Strip[{vmCh}]" : $"Bus[{vmCh - 8}]";
            db = _vm.GetParameter($"{prefix}.Gain");
        }

        // Fader nur bewegen wenn KEIN Fader aktiv ist
        if (!anyFaderActive)
        {
            _xtouch.SetFaderDb(8, db);
        }

        // Gain-Änderung aus Voicemeeter erkennen → dB im Display könnte hier
        // angezeigt werden, aber Main Fader hat kein eigenes Scribble-Display.
        _lastGainValues[8] = db;
    }

    /// <summary>
    /// Aktualisiert die Scribble-Displays aller Kanäle.
    /// Obere Zeile: Kanalname aus Voicemeeter (sofern kein Encoder-Display aktiv).
    /// Untere Zeile: View-Name (sofern kein dB-Wert, Fader-Touch oder Encoder-Display aktiv).
    /// Farben: View-spezifische Farbe hat Vorrang, dann globale Channel-Config, sonst Weiß.
    /// </summary>
    private void UpdateDisplays()
    {
        var colors = new XTouchColor[8];
        var now = DateTime.UtcNow;

        for (int xtCh = 0; xtCh < MidiDevice_ChannelCount(); xtCh++)
        {
            int vmCh = CurrentChannelMapping[xtCh];

            // Encoder-Display-Schutz: Wenn Encoder aktiv ist, Display nicht überschreiben
            bool encoderActive = xtCh < _displayEncoderUntil.Length && _displayEncoderUntil[xtCh] > now;

            // Farbe: View-Override hat Vorrang, sonst globale Channel-Config
            var viewColor = ChannelViews[_currentViewIndex].GetChannelColor(xtCh);
            if (viewColor.HasValue)
                colors[xtCh] = viewColor.Value;
            else if (_config.Channels.TryGetValue(vmCh, out var chConfig))
                colors[xtCh] = chConfig.Color;
            else
                colors[xtCh] = XTouchColor.White;

            // Obere Zeile: Kanalnamen nur setzen wenn kein Encoder-Display aktiv ist
            if (!encoderActive)
            {
                string vmLabel = GetVmLabel(vmCh);
                _xtouch.SetDisplayText(xtCh, 0, vmLabel);
            }

            // Untere Zeile: Ansichtsname, aber NUR wenn kein dB-Wert, kein Encoder-Display
            // und der Fader nicht berührt wird
            bool showingDb = xtCh < _displayDbUntil.Length && _displayDbUntil[xtCh] > now;
            bool isTouched = _xtouch.Channels[xtCh].Fader.IsTouched;
            if (!showingDb && !isTouched && !encoderActive)
            {
                _xtouch.SetDisplayText(xtCh, 1, ChannelViews[_currentViewIndex].Name);
            }
        }

        _xtouch.SetAllDisplayColors(colors);
    }

    /// <summary>
    /// Synchronisiert den Encoder-Ring mit dem aktuellen Wert aus Voicemeeter.
    /// </summary>
    private void SyncEncoderRing(int xtCh, int vmCh)
    {
        var encoder = _xtouch.Channels[xtCh].Encoder;
        var fn = encoder.ActiveFunction;

        if (fn == null)
        {
            // Keine Funktion → Ring ausschalten
            _xtouch.SetEncoderRing(xtCh, 0, XTouchEncoderRingMode.Dot, false);
            return;
        }

        // Aktuellen Wert aus Voicemeeter lesen und in die Funktion übernehmen
        float currentValue = _vm.GetParameter(fn.VmParameter);
        fn.CurrentValue = currentValue;

        // Ring-Position aktualisieren
        encoder.SyncRingToActiveFunction();
        _xtouch.SetEncoderRing(xtCh, encoder.CalculateCcValue(), encoder.RingMode, encoder.RingLed);
    }
}
