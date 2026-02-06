using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Events;
using XTouchVMBridge.Core.Hardware;
using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace XTouchVMBridge.Voicemeeter.Services;

/// <summary>
/// Bridge zwischen X-Touch Extender und Voicemeeter.
/// Entspricht XTouchVM.py (App-Klasse) aus dem Python-Original.
///
/// Läuft als BackgroundService mit 100ms Polling-Intervall.
/// Verwaltet Channel-Mounting, Shortcut-Modi und Level-Meter-Updates.
///
/// Control-Mappings (Fader, Buttons, Encoder) werden aus der Config gelesen,
/// nicht mehr hardcoded. Siehe <see cref="ControlMappingConfig"/>.
///
/// Display-Schutz-Mechanismen:
///   - <c>_displayDbUntil</c>: Zeigt dB-Wert 3s nach Fader-Touch/-Bewegung
///     oder bei Gain-Änderung aus Voicemeeter (z.B. per GUI).
///   - <c>_displayEncoderUntil</c>: Schützt Encoder-Anzeige (Funktionsname + Wert)
///     für 3s nach Drücken/Drehen, damit der Wert in Ruhe abgelesen und
///     eingestellt werden kann, ohne dass der Polling-Loop das Display überschreibt.
/// </summary>
public class VoicemeeterBridge : BackgroundService
{
    private readonly ILogger<VoicemeeterBridge> _logger;
    private readonly IMidiDevice _xtouch;
    private readonly IVoicemeeterService _vm;
    private readonly IConfigurationService _configService;
    private XTouchVMBridgeConfig _config;
    private readonly TaskScheduler _scheduler;

    // ─── Channel Mounting System ────────────────────────────────────

    /// <summary>
    /// Kanal-Ansichten aus der Konfiguration.
    /// Jede Ansicht mappt 8 physische X-Touch-Kanäle auf logische VM-Kanäle.
    /// </summary>
    private List<ChannelViewConfig> ChannelViews => _config.ChannelViews;

    private int _currentViewIndex;

    /// <summary>
    /// Gibt das aktuelle Kanal-Mapping zurück: Index = X-Touch-Kanal (0..7), Wert = VM-Kanal (0..15).
    /// </summary>
    public int[] CurrentChannelMapping => ChannelViews[_currentViewIndex].Channels;

    /// <summary>Index der aktuell aktiven Channel View.</summary>
    public int CurrentViewIndex => _currentViewIndex;

    /// <summary>Name der aktuell aktiven Channel View.</summary>
    public string CurrentViewName => ChannelViews.Count > 0 ? ChannelViews[_currentViewIndex].Name : "";

    /// <summary>Anzahl der verfügbaren Channel Views.</summary>
    public int ViewCount => ChannelViews.Count;

    /// <summary>
    /// Wechselt zur nächsten/vorherigen Channel View.
    /// </summary>
    /// <param name="direction">+1 = nächste, -1 = vorherige</param>
    public void SwitchView(int direction)
    {
        if (ChannelViews.Count == 0) return;
        _currentViewIndex = (_currentViewIndex + direction + ChannelViews.Count) % ChannelViews.Count;

        // Encoder-Funktionen für neue View registrieren
        RegisterEncoderFunctions();

        _needsFullRefresh = true;
        _logger.LogInformation("Ansicht gewechselt zu: {View}", ChannelViews[_currentViewIndex].Name);
    }

    // ─── State ──────────────────────────────────────────────────────

    private VoicemeeterState _vmState = new();
    private readonly double[] _levelCache = new double[VoicemeeterState.TotalChannels];
    private bool _needsFullRefresh = true;

    // ─── Doppel-Touch-Erkennung für Fader (0 dB Reset) ─────────────
    private readonly DateTime[] _lastFaderTouchTime = new DateTime[9]; // 0-7 = Channel, 8 = Main
    private const int DoubleTapThresholdMs = 400;

    // ─── Display-Schutz: dB-Wert wird temporär angezeigt ───────────
    private readonly DateTime[] _displayDbUntil = new DateTime[8]; // Zeitpunkt bis wann dB angezeigt wird

    // ─── Fader-Schutz: Verhindert Zurücksetzen nach Bewegung ───────
    private readonly DateTime[] _faderProtectUntil = new DateTime[9]; // 0-7 = Channel, 8 = Main
    private const int FaderProtectMs = 500; // 500ms Schutz nach Fader-Bewegung

    // ─── Gain-Cache: Erkennt Änderungen aus Voicemeeter (z.B. per GUI) ───
    private readonly double[] _lastGainValues = new double[9]; // 0-7 = Channel, 8 = Main
    private bool _gainCacheInitialized;

    // ─── Encoder-Display-Schutz: Verhindert Überschreiben der Encoder-Anzeige ───
    // Beim Drücken/Drehen eines Encoders wird der Funktionsname (oben) und Wert (unten)
    // für 3 Sekunden angezeigt. Während dieser Zeit überschreibt UpdateDisplays()
    // weder die obere noch die untere Zeile. Jede Encoder-Interaktion verlängert den Schutz.
    private readonly DateTime[] _displayEncoderUntil = new DateTime[8];

    public VoicemeeterBridge(
        ILogger<VoicemeeterBridge> logger,
        IMidiDevice xtouch,
        IVoicemeeterService vm,
        IOptions<XTouchVMBridgeConfig> config,
        IConfigurationService configService)
    {
        _logger = logger;
        _xtouch = xtouch;
        _vm = vm;
        _config = config.Value;
        _configService = configService;
        _scheduler = new TaskScheduler();

        RegisterEncoderFunctions();
    }

    // ─── Encoder-Funktionen registrieren ─────────────────────────────

    /// <summary>
    /// Registriert die Funktionen für jeden Encoder basierend auf der aktuellen View.
    /// Durch Drücken des Encoders wird die nächste Funktion in der Liste aktiviert.
    /// Drehen ändert den Wert der aktiven Funktion.
    /// </summary>
    private void RegisterEncoderFunctions()
    {
        for (int xtCh = 0; xtCh < Math.Min(8, _xtouch.ChannelCount); xtCh++)
        {
            int vmCh = CurrentChannelMapping[xtCh];

            var encoder = _xtouch.Channels[xtCh].Encoder;

            // Bestehende Funktionen entfernen
            encoder.ClearFunctions();

            if (!_config.Mappings.TryGetValue(vmCh, out var mapping))
                continue;

            if (mapping.EncoderFunctions.Count == 0)
                continue;

            foreach (var fn in mapping.EncoderFunctions)
            {
                encoder.AddFunction(new EncoderFunction(
                    fn.Label, fn.Parameter, fn.Min, fn.Max, fn.Step, fn.Unit));
            }

            encoder.RingMode = XTouchEncoderRingMode.Pan;
        }
    }

    /// <summary>
    /// Lädt die Config neu und re-registriert die Encoder-Funktionen.
    /// Wird aufgerufen wenn die Config im Panel geändert wurde.
    /// </summary>
    public void ReloadMappings()
    {
        _config = _configService.Load();
        RegisterEncoderFunctions();
        _needsFullRefresh = true;
        _logger.LogInformation("Mappings neu geladen.");
    }

    // ─── BackgroundService ──────────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VoicemeeterBridge gestartet.");

        RegisterCallbacks();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Level-Updates (immer)
                UpdateLevels();

                // Parameter-Updates (nur wenn dirty)
                if (_vm.IsParameterDirty || _needsFullRefresh)
                {
                    UpdateParameters();
                    _needsFullRefresh = false;
                }

                // Geplante Tasks ausführen
                _scheduler.RunDue();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler im VoicemeeterBridge-Loop.");
            }

            await Task.Delay(100, stoppingToken);
        }

        _logger.LogInformation("VoicemeeterBridge gestoppt.");
    }

    // ─── Callback Registration ──────────────────────────────────────

    private void RegisterCallbacks()
    {
        _xtouch.FaderChanged += OnFaderChanged;
        _xtouch.ButtonChanged += OnButtonChanged;
        _xtouch.EncoderRotated += OnEncoderRotated;
        _xtouch.EncoderPressed += OnEncoderPressed;
        _xtouch.FaderTouched += OnFaderTouched;
    }

    // ─── Helpers: Mapping-Zugriff ────────────────────────────────────

    /// <summary>Gibt das Mapping für einen VM-Kanal zurück (oder null).</summary>
    private ControlMappingConfig? GetMapping(int vmChannel)
    {
        _config.Mappings.TryGetValue(vmChannel, out var mapping);
        return mapping;
    }

    /// <summary>Gibt das ButtonMapping für einen VM-Kanal + ButtonType zurück.</summary>
    private ButtonMappingConfig? GetButtonMapping(int vmChannel, XTouchButtonType buttonType)
    {
        var mapping = GetMapping(vmChannel);
        if (mapping == null) return null;

        string btnKey = buttonType.ToString();
        mapping.Buttons.TryGetValue(btnKey, out var btnMap);
        return btnMap;
    }

    // ─── Update Methods ─────────────────────────────────────────────

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
                else
                {
                    _xtouch.SetButtonLed(xtCh, btnType, LedState.Off);
                }
            }

            // Encoder-Ring synchronisieren
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
    /// Liest den Label eines VM-Kanals direkt aus Voicemeeter.
    /// Strip[0..7].Label für Inputs, Bus[0..7].Label für Outputs.
    /// Fallback auf Config-Name oder "Ch N".
    /// </summary>
    private string GetVmLabel(int vmChannel)
    {
        try
        {
            string paramName = vmChannel < 8
                ? $"Strip[{vmChannel}].Label"
                : $"Bus[{vmChannel - 8}].Label";

            string label = _vm.GetParameterString(paramName);

            if (!string.IsNullOrWhiteSpace(label))
                return label.Length > 7 ? label[..7] : label;
        }
        catch
        {
            // Fehler beim Lesen → Fallback
        }

        // Fallback: Config-Name oder generischer Name
        if (_config.Channels.TryGetValue(vmChannel, out var chConfig))
            return chConfig.Name;

        return $"Ch {vmChannel + 1}";
    }

    // ─── Callbacks (X-Touch → Voicemeeter) ──────────────────────────

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

        if (btnMap != null)
        {
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

    // ─── Helpers ────────────────────────────────────────────────────

    private int MidiDevice_ChannelCount() => Math.Min(_xtouch.ChannelCount, CurrentChannelMapping.Length);
}

/// <summary>
/// Einfacher Task-Scheduler für verzögerte Aktionen.
/// Entspricht der Scheduler-Klasse aus dem Python-Original.
/// </summary>
internal class TaskScheduler
{
    private readonly Dictionary<string, (Action Task, DateTime DueTime)> _tasks = new();

    public void AddTask(Action task, TimeSpan delay, string identifier)
    {
        _tasks[identifier] = (task, DateTime.UtcNow + delay);
    }

    public void CancelTask(string identifier)
    {
        _tasks.Remove(identifier);
    }

    public void RunDue()
    {
        var now = DateTime.UtcNow;
        var dueTasks = _tasks.Where(kv => kv.Value.DueTime <= now).ToList();

        foreach (var (key, (task, _)) in dueTasks)
        {
            _tasks.Remove(key);
            try { task(); }
            catch { /* Fehler in Scheduled Tasks werden ignoriert */ }
        }
    }

    public void Clear() => _tasks.Clear();
}
