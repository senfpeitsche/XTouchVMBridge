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
        _needsFullRefresh = true;
        _logger.LogInformation("Ansicht gewechselt zu: {View}", ChannelViews[_currentViewIndex].Name);
    }

    // ─── State ──────────────────────────────────────────────────────

    private VoicemeeterState _vmState = new();
    private readonly double[] _levelCache = new double[VoicemeeterState.TotalChannels];
    private bool _needsFullRefresh = true;

    // ─── Shortcut System ────────────────────────────────────────────

    private int _shortcutModeIndex;
    private readonly List<ShortcutMode> _shortcutModes = new()
    {
        new("DESKTOP", "Desktop-Audio-Routing"),
        new("VR",      "VR-Audio-Routing")
    };

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
    /// Registriert die Funktionen für jeden Encoder (pro Kanal) aus der Config.
    /// Durch Drücken des Encoders wird die nächste Funktion in der Liste aktiviert.
    /// Drehen ändert den Wert der aktiven Funktion.
    /// </summary>
    private void RegisterEncoderFunctions()
    {
        // Encoder 0: bleibt für Ansichtswechsel (keine Funktionen)
        // Encoder 2: bleibt für Shortcut-Modus (keine Funktionen)

        for (int xtCh = 0; xtCh < Math.Min(8, _xtouch.ChannelCount); xtCh++)
        {
            // Kanäle 0 und 2 bleiben für Navigation/Shortcuts
            if (xtCh == 0 || xtCh == 2) continue;

            int vmCh = ChannelViews[0].Channels[xtCh]; // Home-View Kanal

            if (!_config.Mappings.TryGetValue(vmCh, out var mapping))
                continue;

            if (mapping.EncoderFunctions.Count == 0)
                continue;

            var encoder = _xtouch.Channels[xtCh].Encoder;

            // Bestehende Funktionen entfernen (für Reload)
            encoder.ClearFunctions();

            foreach (var fn in mapping.EncoderFunctions)
            {
                encoder.AddFunction(new EncoderFunction(
                    fn.Label, fn.Parameter, fn.Min, fn.Max, fn.Step, fn.Unit));
            }

            encoder.RingMode = XTouchEncoderRingMode.Pan;
            encoder.SyncRingToActiveFunction();
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

        for (int xtCh = 0; xtCh < MidiDevice_ChannelCount(); xtCh++)
        {
            int vmCh = CurrentChannelMapping[xtCh];
            var mapping = GetMapping(vmCh);

            // Fader-Position synchronisieren (aus Mapping oder Fallback)
            if (!_xtouch.Channels[xtCh].Fader.IsTouched)
            {
                if (mapping?.Fader != null)
                {
                    float gain = _vm.GetParameter(mapping.Fader.Parameter);
                    _xtouch.SetFaderDb(xtCh, gain);
                }
                else
                {
                    _xtouch.SetFaderDb(xtCh, _vmState.Gains[vmCh]);
                }
            }

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
        }

        UpdateDisplays();
    }

    private void UpdateDisplays()
    {
        var colors = new XTouchColor[8];

        for (int xtCh = 0; xtCh < MidiDevice_ChannelCount(); xtCh++)
        {
            int vmCh = CurrentChannelMapping[xtCh];

            // Name aus Voicemeeter-Label lesen
            string vmLabel = GetVmLabel(vmCh);
            _xtouch.SetDisplayText(xtCh, 0, vmLabel);

            // Farbe: View-Override hat Vorrang, sonst globale Channel-Config
            var viewColor = ChannelViews[_currentViewIndex].GetChannelColor(xtCh);
            if (viewColor.HasValue)
                colors[xtCh] = viewColor.Value;
            else if (_config.Channels.TryGetValue(vmCh, out var chConfig))
                colors[xtCh] = chConfig.Color;
            else
                colors[xtCh] = XTouchColor.White;

            // Untere Zeile: Ansichtsname oder dB-Wert
            _xtouch.SetDisplayText(xtCh, 1, ChannelViews[_currentViewIndex].Name);
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

        // dB-Wert temporär anzeigen
        string dbText = e.Db <= -60 ? " -inf " : $"{e.Db:F1}dB";
        _xtouch.SetDisplayText(e.Channel, 1, dbText);

        _scheduler.AddTask(
            () => _xtouch.SetDisplayText(e.Channel, 1, ChannelViews[_currentViewIndex].Name),
            TimeSpan.FromSeconds(2),
            $"fader_display_{e.Channel}");
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

                // Wert im Display anzeigen
                _xtouch.SetDisplayText(e.Channel, 1, fn.FormatValue());

                _logger.LogDebug("Encoder {Ch} [{Fn}]: {Val}", e.Channel + 1, fn.Name, fn.FormatValue());

                // Nach 2s wieder den Funktionsnamen anzeigen
                _scheduler.AddTask(
                    () => ShowEncoderFunctionName(e.Channel),
                    TimeSpan.FromSeconds(2),
                    $"encoder_display_{e.Channel}");
            }
            return;
        }

        // Fallback: Speziallogik ohne Funktionsliste
        switch (e.Channel)
        {
            case 0:
                // Kanal 0 Encoder: Ansicht wechseln
                _currentViewIndex = (_currentViewIndex + e.Ticks + ChannelViews.Count) % ChannelViews.Count;
                _needsFullRefresh = true;
                _logger.LogDebug("Ansicht gewechselt zu: {View}", ChannelViews[_currentViewIndex].Name);
                break;
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
                encoder.SyncRingToActiveFunction();
                _xtouch.SetEncoderRing(e.Channel, encoder.CalculateCcValue(), encoder.RingMode, encoder.RingLed);

                // Aktive Funktion und Wert anzeigen
                _xtouch.SetDisplayText(e.Channel, 1, fn.Name);

                _logger.LogInformation("Encoder {Ch}: Funktion → {Fn} ({Val})",
                    e.Channel + 1, fn.Name, fn.FormatValue());

                // Nach 1.5s den Wert anzeigen, dann nach weiteren 2s den Funktionsnamen
                _scheduler.AddTask(
                    () => _xtouch.SetDisplayText(e.Channel, 1, fn.FormatValue()),
                    TimeSpan.FromSeconds(1.5),
                    $"encoder_display_{e.Channel}");

                _scheduler.AddTask(
                    () => ShowEncoderFunctionName(e.Channel),
                    TimeSpan.FromSeconds(3.5),
                    $"encoder_name_{e.Channel}");
            }
            return;
        }

        // Fallback: Speziallogik ohne Funktionsliste
        switch (e.Channel)
        {
            case 0:
                // Encoder 0 Press: zurück zur Home-Ansicht
                _currentViewIndex = 0;
                _needsFullRefresh = true;
                break;

            case 2:
                // Encoder 2 Press: Shortcut-Modus wechseln
                _shortcutModeIndex = (_shortcutModeIndex + 1) % _shortcutModes.Count;
                var mode = _shortcutModes[_shortcutModeIndex];
                _logger.LogInformation("Shortcut-Modus: {Mode}", mode.Name);
                break;
        }
    }

    /// <summary>
    /// Zeigt den Namen der aktiven Encoder-Funktion im Display an.
    /// Format: "►HIGH" (Pfeil zeigt aktive Funktion).
    /// </summary>
    private void ShowEncoderFunctionName(int xtCh)
    {
        var fn = _xtouch.Channels[xtCh].Encoder.ActiveFunction;
        if (fn != null)
        {
            _xtouch.SetDisplayText(xtCh, 1, $">{fn.Name}");
        }
    }

    private void OnFaderTouched(object? sender, FaderTouchEventArgs e)
    {
        if (e.IsTouched)
        {
            // Bei Touch: aktuellen dB-Wert anzeigen
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
            // Bei Release: Ansichtsname wiederherstellen
            _xtouch.SetDisplayText(e.Channel, 1, ChannelViews[_currentViewIndex].Name);
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private int MidiDevice_ChannelCount() => Math.Min(_xtouch.ChannelCount, CurrentChannelMapping.Length);

    // ─── Inner Types ────────────────────────────────────────────────

    /// <summary>Definiert einen Shortcut-Modus.</summary>
    private record ShortcutMode(string Name, string Description);
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
