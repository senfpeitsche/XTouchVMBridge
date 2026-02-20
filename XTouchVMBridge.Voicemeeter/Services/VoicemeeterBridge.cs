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
/// LÃ¤uft als BackgroundService mit 100ms Polling-Intervall.
/// Verwaltet Channel-Mounting, Shortcut-Modi und Level-Meter-Updates.
///
/// Control-Mappings (Fader, Buttons, Encoder) werden aus der Config gelesen,
/// nicht mehr hardcoded. Siehe <see cref="ControlMappingConfig"/>.
///
/// Display-Schutz-Mechanismen:
///   - <c>_displayDbUntil</c>: Zeigt dB-Wert 3s nach Fader-Touch/-Bewegung
///     oder bei Gain-Ã„nderung aus Voicemeeter (z.B. per GUI).
///   - <c>_displayEncoderUntil</c>: SchÃ¼tzt Encoder-Anzeige (Funktionsname + Wert)
///     fÃ¼r 3s nach DrÃ¼cken/Drehen, damit der Wert in Ruhe abgelesen und
///     eingestellt werden kann, ohne dass der Polling-Loop das Display Ã¼berschreibt.
///
/// Partial-Klassen:
///   - VoicemeeterBridge.Sync.cs      â†’ Level-Updates, Parameter-Sync, Display-Updates
///   - VoicemeeterBridge.Callbacks.cs  â†’ X-Touch Event-Callbacks (Fader, Button, Encoder, Touch)
/// </summary>
public partial class VoicemeeterBridge : BackgroundService
{
    private readonly ILogger<VoicemeeterBridge> _logger;
    private readonly IMidiDevice _xtouch;
    private readonly IVoicemeeterService _vm;
    private readonly IConfigurationService _configService;
    private XTouchVMBridgeConfig _config;
    private readonly TaskScheduler _scheduler;

    // â”€â”€â”€ Channel Mounting System â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Kanal-Ansichten aus der Konfiguration.
    /// Jede Ansicht mappt 8 physische X-Touch-KanÃ¤le auf logische VM-KanÃ¤le.
    /// </summary>
    private List<ChannelViewConfig> ChannelViews => _config.ChannelViews;

    private int _currentViewIndex;

    /// <summary>
    /// Gibt das aktuelle Kanal-Mapping zurÃ¼ck: Index = X-Touch-Kanal (0..7), Wert = VM-Kanal (0..15).
    /// </summary>
    public int[] CurrentChannelMapping => ChannelViews[_currentViewIndex].Channels;

    /// <summary>Index der aktuell aktiven Channel View.</summary>
    public int CurrentViewIndex => _currentViewIndex;

    /// <summary>Name der aktuell aktiven Channel View.</summary>
    public string CurrentViewName => ChannelViews.Count > 0 ? ChannelViews[_currentViewIndex].Name : "";

    /// <summary>Anzahl der verfÃ¼gbaren Channel Views.</summary>
    public int ViewCount => ChannelViews.Count;

    /// <summary>
    /// Wechselt zur nÃ¤chsten/vorherigen Channel View.
    /// </summary>
    /// <param name="direction">+1 = nÃ¤chste, -1 = vorherige</param>
    public void SwitchView(int direction)
    {
        if (ChannelViews.Count == 0) return;
        _currentViewIndex = (_currentViewIndex + direction + ChannelViews.Count) % ChannelViews.Count;

        for (int ch = 0; ch < MidiDevice_ChannelCount(); ch++)
            _xtouch.SetLevelMeter(ch, 0);
        _forceLevelRefresh = true;
        _logger.LogDebug("VU-Meter nach View-Wechsel zurückgesetzt.");

        // Encoder-Funktionen fÃ¼r neue View registrieren
        RegisterEncoderFunctions();

        _needsFullRefresh = true;
        _logger.LogInformation("Ansicht gewechselt zu: {View}", ChannelViews[_currentViewIndex].Name);
    }

    // â”€â”€â”€ State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private VoicemeeterState _vmState = new();
    private readonly double[] _levelCache = new double[VoicemeeterState.TotalChannels];
    private bool _forceLevelRefresh;
    private bool _needsFullRefresh = true;
    private volatile bool _xtouchReconnected;

    // â”€â”€â”€ Doppel-Touch-Erkennung fÃ¼r Fader (0 dB Reset) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private readonly DateTime[] _lastFaderTouchTime = new DateTime[9]; // 0-7 = Channel, 8 = Main
    private const int DoubleTapThresholdMs = 400;

    // â”€â”€â”€ Display-Schutz: dB-Wert wird temporÃ¤r angezeigt â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private readonly DateTime[] _displayDbUntil = new DateTime[8]; // Zeitpunkt bis wann dB angezeigt wird

    // â”€â”€â”€ Fader-Schutz: Verhindert ZurÃ¼cksetzen nach Bewegung â”€â”€â”€â”€â”€â”€â”€
    private readonly DateTime[] _faderProtectUntil = new DateTime[9]; // 0-7 = Channel, 8 = Main
    private const int FaderProtectMs = 500; // 500ms Schutz nach Fader-Bewegung

    // â”€â”€â”€ Gain-Cache: Erkennt Ã„nderungen aus Voicemeeter (z.B. per GUI) â”€â”€â”€
    private readonly double[] _lastGainValues = new double[9]; // 0-7 = Channel, 8 = Main
    private bool _gainCacheInitialized;

    // â”€â”€â”€ Encoder-Display-Schutz: Verhindert Ãœberschreiben der Encoder-Anzeige â”€â”€â”€
    // Beim DrÃ¼cken/Drehen eines Encoders wird der Funktionsname (oben) und Wert (unten)
    // fÃ¼r 3 Sekunden angezeigt. WÃ¤hrend dieser Zeit Ã¼berschreibt UpdateDisplays()
    // weder die obere noch die untere Zeile. Jede Encoder-Interaktion verlÃ¤ngert den Schutz.
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

    // â”€â”€â”€ Encoder-Funktionen registrieren â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Registriert die Funktionen fÃ¼r jeden Encoder basierend auf der aktuellen View.
    /// Durch DrÃ¼cken des Encoders wird die nÃ¤chste Funktion in der Liste aktiviert.
    /// Drehen Ã¤ndert den Wert der aktiven Funktion.
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
    /// und Display fÃ¼r die angegebene Dauer Ã¼berschreibt. Wird vom PanelView
    /// aufgerufen wenn der Encoder per Mausrad gesteuert wird.
    /// </summary>
    public void SuppressEncoderSync(int channel, TimeSpan duration)
    {
        if (channel >= 0 && channel < _displayEncoderUntil.Length)
            _displayEncoderUntil[channel] = DateTime.UtcNow + duration;
    }

    /// <summary>
    /// LÃ¤dt die Config neu und re-registriert die Encoder-Funktionen.
    /// Wird aufgerufen wenn die Config im Panel geÃ¤ndert wurde.
    /// </summary>
    public void ReloadMappings()
    {
        _config = _configService.Load();
        RegisterEncoderFunctions();
        _needsFullRefresh = true;
        _logger.LogInformation("Mappings neu geladen.");
    }

    // â”€â”€â”€ BackgroundService â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VoicemeeterBridge gestartet.");

        RegisterCallbacks();

        // Warten bis Voicemeeter und X-Touch verbunden sind.
        // Die Bridge startet als HostedService BEVOR Connect() aufgerufen wird,
        // daher mÃ¼ssen wir hier auf beide Verbindungen warten.
        _logger.LogDebug("Warte auf Voicemeeter- und X-Touch-Verbindung...");
        while (!stoppingToken.IsCancellationRequested && (!_vm.IsConnected || !_xtouch.IsConnected))
        {
            await Task.Delay(200, stoppingToken);
        }

        if (stoppingToken.IsCancellationRequested) return;

        // Beide Verbindungen stehen â€” ersten Dirty-Check konsumieren und
        // Full Refresh erzwingen damit Fader/LCDs sofort gesetzt werden.
        // Kurz warten damit das X-Touch nach Initialisierung bereit ist.
        await Task.Delay(500, stoppingToken);
        _ = _vm.IsParameterDirty; // Dirty-Flag konsumieren (VBVMR_IsParametersDirty pollt intern)
        _needsFullRefresh = true;
        _xtouchReconnected = false; // Initiales Connect ist kein "Reconnect"
        _logger.LogInformation("Voicemeeter und X-Touch verbunden â€” starte initialen Sync.");

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
                    _logger.LogInformation("X-Touch (re)connected â€” erzwinge Full Refresh.");
                    await Task.Delay(300, stoppingToken); // GerÃ¤t stabilisieren lassen
                }

                if (!_xtouch.IsConnected)
                {
                    // X-Touch getrennt â€” warten statt busy-loop
                    await Task.Delay(500, stoppingToken);
                    continue;
                }

                // Level-Updates (immer, unabhÃ¤ngig vom Dirty-Flag)
                UpdateLevels();

                // Parameter-Updates (nur wenn dirty oder Full Refresh ansteht)
                if (_needsFullRefresh || _vm.IsParameterDirty)
                {
                    UpdateParameters();
                    _needsFullRefresh = false;
                }

                // Geplante Tasks ausfÃ¼hren
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

    // â”€â”€â”€ Callback Registration â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
            _logger.LogDebug("Bridge: X-Touch Verbindungsstatus â†’ verbunden (Full Refresh wird angefordert).");
        }
        else
        {
            _logger.LogDebug("Bridge: X-Touch Verbindungsstatus â†’ getrennt.");
        }
    }

    // â”€â”€â”€ Helpers: Mapping-Zugriff â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Gibt das Mapping fÃ¼r einen VM-Kanal zurÃ¼ck (oder null).</summary>
    private ControlMappingConfig? GetMapping(int vmChannel)
    {
        _config.Mappings.TryGetValue(vmChannel, out var mapping);
        return mapping;
    }

    /// <summary>Gibt das ButtonMapping fÃ¼r einen VM-Kanal + ButtonType zurÃ¼ck.</summary>
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
    /// Strip[0..7].Label fÃ¼r Inputs, Bus[0..7].Label fÃ¼r Outputs.
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
            // Fehler beim Lesen â†’ Fallback
        }

        // Fallback: Config-Name oder generischer Name
        if (_config.Channels.TryGetValue(vmChannel, out var chConfig))
            return chConfig.Name;

        return $"Ch {vmChannel + 1}";
    }

    private int MidiDevice_ChannelCount() => Math.Min(_xtouch.ChannelCount, CurrentChannelMapping.Length);
}

/// <summary>
/// Einfacher Task-Scheduler fÃ¼r verzÃ¶gerte Aktionen.
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



