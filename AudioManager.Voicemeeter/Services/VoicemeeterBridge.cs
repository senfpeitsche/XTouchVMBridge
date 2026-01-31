using AudioManager.Core.Enums;
using AudioManager.Core.Events;
using AudioManager.Core.Hardware;
using AudioManager.Core.Interfaces;
using AudioManager.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AudioManager.Voicemeeter.Services;

/// <summary>
/// Bridge zwischen X-Touch Extender und Voicemeeter.
/// Entspricht XTouchVM.py (App-Klasse) aus dem Python-Original.
///
/// Läuft als BackgroundService mit 100ms Polling-Intervall.
/// Verwaltet Channel-Mounting, Shortcut-Modi und Level-Meter-Updates.
/// </summary>
public class VoicemeeterBridge : BackgroundService
{
    private readonly ILogger<VoicemeeterBridge> _logger;
    private readonly IMidiDevice _xtouch;
    private readonly IVoicemeeterService _vm;
    private readonly AudioManagerConfig _config;
    private readonly TaskScheduler _scheduler;

    // ─── Channel Mounting System ────────────────────────────────────

    /// <summary>
    /// Vordefinierte Kanal-Ansichten.
    /// Jede Ansicht mappt 8 physische X-Touch-Kanäle auf logische VM-Kanäle.
    /// </summary>
    private readonly List<ChannelView> _channelViews = new()
    {
        new("Home",    new[] { 3, 4, 5, 6, 7, 9, 10, 12 }),
        new("Outputs", new[] { 8, 9, 10, 11, 12, 13, 14, 15 }),
        new("Inputs",  new[] { 0, 1, 2, 3, 4, 5, 6, 7 })
    };

    private int _currentViewIndex;
    private int[] CurrentChannelMapping => _channelViews[_currentViewIndex].Channels;

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
        IOptions<AudioManagerConfig> config)
    {
        _logger = logger;
        _xtouch = xtouch;
        _vm = vm;
        _config = config.Value;
        _scheduler = new TaskScheduler();

        RegisterEncoderFunctions();
    }

    // ─── Encoder-Funktionen registrieren ─────────────────────────────

    /// <summary>
    /// Registriert die Funktionen für jeden Encoder (pro Kanal).
    /// Durch Drücken des Encoders wird die nächste Funktion in der Liste aktiviert.
    /// Drehen ändert den Wert der aktiven Funktion.
    /// </summary>
    private void RegisterEncoderFunctions()
    {
        // Encoder 0: bleibt für Ansichtswechsel (keine Funktionen)
        // Encoder 2: bleibt für Shortcut-Modus (keine Funktionen)

        // Encoder 1,3,4,5,6,7: EQ-Funktionen (High/Mid/Low) pro Kanal
        int[] eqEncoderChannels = { 1, 3, 4, 5, 6, 7 };
        foreach (int xtCh in eqEncoderChannels)
        {
            var encoder = _xtouch.Channels[xtCh].Encoder;
            int vmCh = _channelViews[0].Channels[xtCh]; // Home-View Kanal

            encoder.AddFunctions(new[]
            {
                new EncoderFunction("HIGH", $"Strip[{vmCh}].EQGain3", -12, 12, 0.5, "dB"),
                new EncoderFunction("MID",  $"Strip[{vmCh}].EQGain2", -12, 12, 0.5, "dB"),
                new EncoderFunction("LOW",  $"Strip[{vmCh}].EQGain1", -12, 12, 0.5, "dB"),
                new EncoderFunction("PAN",  $"Strip[{vmCh}].Pan_x",   -0.5, 0.5, 0.05, ""),
                new EncoderFunction("GAIN", $"Strip[{vmCh}].Gain",    -60, 12, 0.5, "dB")
            });

            encoder.RingMode = XTouchEncoderRingMode.Pan;
            encoder.SyncRingToActiveFunction();
        }
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

            // Fader-Position synchronisieren
            double gain = _vmState.Gains[vmCh];
            if (!_xtouch.Channels[xtCh].Fader.IsTouched)
            {
                _xtouch.SetFaderDb(xtCh, gain);
            }

            // Mute-Button LED
            _xtouch.SetButtonLed(xtCh, XTouchButtonType.Mute,
                _vmState.Mutes[vmCh] ? LedState.On : LedState.Off);

            // Solo-Button LED (nur Strips)
            if (_vm.IsStrip(vmCh))
            {
                _xtouch.SetButtonLed(xtCh, XTouchButtonType.Solo,
                    _vmState.Solos[vmCh] ? LedState.On : LedState.Off);
            }
            else
            {
                _xtouch.SetButtonLed(xtCh, XTouchButtonType.Solo, LedState.Off);
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

            if (_config.Channels.TryGetValue(vmCh, out var chConfig))
            {
                _xtouch.SetDisplayText(xtCh, 0, chConfig.Name);
                colors[xtCh] = chConfig.Color;
            }
            else
            {
                _xtouch.SetDisplayText(xtCh, 0, $"Ch {vmCh + 1}");
                colors[xtCh] = XTouchColor.White;
            }

            // Untere Zeile: Ansichtsname oder dB-Wert
            _xtouch.SetDisplayText(xtCh, 1, _channelViews[_currentViewIndex].Name);
        }

        _xtouch.SetAllDisplayColors(colors);
    }

    // ─── Callbacks (X-Touch → Voicemeeter) ──────────────────────────

    private void OnFaderChanged(object? sender, FaderEventArgs e)
    {
        int vmCh = CurrentChannelMapping[e.Channel];
        double db = Math.Max(e.Db, -60.0); // Clamp wie im Python-Original
        _vm.SetGain(vmCh, db);

        // dB-Wert temporär anzeigen
        string dbText = e.Db <= -60 ? " -inf " : $"{e.Db:F1}dB";
        _xtouch.SetDisplayText(e.Channel, 1, dbText);

        _scheduler.AddTask(
            () => _xtouch.SetDisplayText(e.Channel, 1, _channelViews[_currentViewIndex].Name),
            TimeSpan.FromSeconds(2),
            $"fader_display_{e.Channel}");
    }

    private void OnButtonChanged(object? sender, ButtonEventArgs e)
    {
        if (!e.IsPressed) return; // Nur auf Press reagieren

        int vmCh = CurrentChannelMapping[e.Channel];

        switch (e.ButtonType)
        {
            case XTouchButtonType.Mute:
                bool currentMute = _vmState.Mutes[vmCh];
                _vm.SetMute(vmCh, !currentMute);
                _needsFullRefresh = true;
                break;

            case XTouchButtonType.Solo:
                if (_vm.IsStrip(vmCh))
                {
                    bool currentSolo = _vmState.Solos[vmCh];
                    _vm.SetSolo(vmCh, !currentSolo);
                    _needsFullRefresh = true;
                }
                break;
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
                _currentViewIndex = (_currentViewIndex + e.Ticks + _channelViews.Count) % _channelViews.Count;
                _needsFullRefresh = true;
                _logger.LogDebug("Ansicht gewechselt zu: {View}", _channelViews[_currentViewIndex].Name);
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
            double db = _vmState.Gains[vmCh];
            string dbText = db <= -60 ? " -inf " : $"{db:F1}dB";
            _xtouch.SetDisplayText(e.Channel, 1, dbText);
        }
        else
        {
            // Bei Release: Ansichtsname wiederherstellen
            _xtouch.SetDisplayText(e.Channel, 1, _channelViews[_currentViewIndex].Name);
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private int MidiDevice_ChannelCount() => Math.Min(_xtouch.ChannelCount, CurrentChannelMapping.Length);

    // ─── Inner Types ────────────────────────────────────────────────

    /// <summary>Definiert eine Kanal-Ansicht (Name + Mapping auf VM-Kanäle).</summary>
    private record ChannelView(string Name, int[] Channels);

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
