using XTouchVMBridge.Core.Enums;
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
///
/// Partial-Klassen:
///   - VoicemeeterBridge.Sync.cs      → Level-Updates, Parameter-Sync, Display-Updates
///   - VoicemeeterBridge.Callbacks.cs  → X-Touch Event-Callbacks (Fader, Button, Encoder, Touch)
/// </summary>
public partial class VoicemeeterBridge : BackgroundService
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
    private volatile bool _xtouchReconnected;

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
    private bool _isRecorderActive;

    /// <summary>
    /// Interner Recorder-Status fuer Toggle/LED-Sync.
    /// Voicemeeter liefert Recorder.Record nicht zuverlaessig als lesbaren Status.
    /// </summary>
    public bool IsRecorderActive => _isRecorderActive;

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
    /// Setzt den Encoder-Schutz: Verhindert dass der Bridge-Sync den Encoder-Ring
    /// und Display für die angegebene Dauer überschreibt. Wird vom PanelView
    /// aufgerufen wenn der Encoder per Mausrad gesteuert wird.
    /// </summary>
    public void SuppressEncoderSync(int channel, TimeSpan duration)
    {
        if (channel >= 0 && channel < _displayEncoderUntil.Length)
            _displayEncoderUntil[channel] = DateTime.UtcNow + duration;
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

        // Warten bis Voicemeeter und X-Touch verbunden sind.
        // Die Bridge startet als HostedService BEVOR Connect() aufgerufen wird,
        // daher müssen wir hier auf beide Verbindungen warten.
        _logger.LogDebug("Warte auf Voicemeeter- und X-Touch-Verbindung...");
        while (!stoppingToken.IsCancellationRequested && (!_vm.IsConnected || !_xtouch.IsConnected))
        {
            await Task.Delay(200, stoppingToken);
        }

        if (stoppingToken.IsCancellationRequested) return;

        // Beide Verbindungen stehen — ersten Dirty-Check konsumieren und
        // Full Refresh erzwingen damit Fader/LCDs sofort gesetzt werden.
        // Kurz warten damit das X-Touch nach Initialisierung bereit ist.
        await Task.Delay(500, stoppingToken);
        _ = _vm.IsParameterDirty; // Dirty-Flag konsumieren (VBVMR_IsParametersDirty pollt intern)
        _needsFullRefresh = true;
        _xtouchReconnected = false; // Initiales Connect ist kein "Reconnect"
        _logger.LogInformation("Voicemeeter und X-Touch verbunden — starte initialen Sync.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Reconnect erkannt (via ConnectionStateChanged-Event)?
                // Das Event wird auch bei Reconnects durch den AudioDeviceMonitor gefeuert.
                if (_xtouchReconnected)
                {
                    _xtouchReconnected = false;
                    _needsFullRefresh = true;
                    _ = _vm.IsParameterDirty; // Dirty-Flag konsumieren
                    _logger.LogInformation("X-Touch (re)connected — erzwinge Full Refresh.");
                    await Task.Delay(300, stoppingToken); // Gerät stabilisieren lassen
                }

                if (!_xtouch.IsConnected)
                {
                    // X-Touch getrennt — warten statt busy-loop
                    await Task.Delay(500, stoppingToken);
                    continue;
                }

                // Level-Updates (immer, unabhängig vom Dirty-Flag)
                UpdateLevels();

                // Parameter-Updates (nur wenn dirty oder Full Refresh ansteht)
                if (_needsFullRefresh || _vm.IsParameterDirty)
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
        _xtouch.ConnectionStateChanged += OnXTouchConnectionChanged;
    }

    private void OnXTouchConnectionChanged(object? sender, bool connected)
    {
        if (connected)
        {
            _xtouchReconnected = true;
            _logger.LogDebug("Bridge: X-Touch Verbindungsstatus → verbunden (Full Refresh wird angefordert).");
        }
        else
        {
            _logger.LogDebug("Bridge: X-Touch Verbindungsstatus → getrennt.");
        }
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
