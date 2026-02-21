using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Hardware;
using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace XTouchVMBridge.Voicemeeter.Services;

/// <summary>
/// Runtime bridge between X-Touch and Voicemeeter.
/// Handles polling sync, view mapping, and hardware callback routing.
/// </summary>
public partial class VoicemeeterBridge : BackgroundService
{
    private readonly ILogger<VoicemeeterBridge> _logger;
    private readonly IMidiDevice _xtouch;
    private readonly IVoicemeeterService _vm;
    private readonly IConfigurationService _configService;
    private XTouchVMBridgeConfig _config;
    private readonly TaskScheduler _scheduler;


    private List<ChannelViewConfig> ChannelViews => _config.ChannelViews;

    private int _currentViewIndex;

    public int[] CurrentChannelMapping => ChannelViews[_currentViewIndex].Channels;

    public int CurrentViewIndex => _currentViewIndex;

    public string CurrentViewName => ChannelViews.Count > 0 ? ChannelViews[_currentViewIndex].Name : "";

    public int ViewCount => ChannelViews.Count;

    public void SwitchView(int direction)
    {
        if (ChannelViews.Count == 0) return;
        _currentViewIndex = (_currentViewIndex + direction + ChannelViews.Count) % ChannelViews.Count;

        for (int ch = 0; ch < MidiDevice_ChannelCount(); ch++)
            _xtouch.SetLevelMeter(ch, 0);
        _forceLevelRefresh = true;
        _logger.LogDebug("VU-Meter nach View-Wechsel zur�ckgesetzt.");

        RegisterEncoderFunctions();

        _needsFullRefresh = true;
        _logger.LogInformation("Ansicht gewechselt zu: {View}", ChannelViews[_currentViewIndex].Name);
    }


    private VoicemeeterState _vmState = new();
    private readonly double[] _levelCache = new double[VoicemeeterState.TotalChannels];
    private bool _forceLevelRefresh;
    private bool _needsFullRefresh = true;
    private volatile bool _xtouchReconnected;

    private readonly DateTime[] _lastFaderTouchTime = new DateTime[9]; // 0-7 = strip faders, 8 = main fader
    private const int DoubleTapThresholdMs = 400;

    private readonly DateTime[] _displayDbUntil = new DateTime[8]; // Keep dB readout active until this UTC timestamp.

    private readonly DateTime[] _faderProtectUntil = new DateTime[9]; // 0-7 = strip faders, 8 = main fader
    private const int FaderProtectMs = 500; // Ignore sync writeback for 500 ms after local movement.

    private readonly double[] _lastGainValues = new double[9]; // 0-7 = strip faders, 8 = main fader
    private bool _gainCacheInitialized;

    private readonly DateTime[] _displayEncoderUntil = new DateTime[8];
    private bool _isRecorderActive;

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


    private void RegisterEncoderFunctions()
    {
        for (int xtCh = 0; xtCh < Math.Min(8, _xtouch.ChannelCount); xtCh++)
        {
            int vmCh = CurrentChannelMapping[xtCh];

            var encoder = _xtouch.Channels[xtCh].Encoder;

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

    public void SuppressEncoderSync(int channel, TimeSpan duration)
    {
        if (channel >= 0 && channel < _displayEncoderUntil.Length)
            _displayEncoderUntil[channel] = DateTime.UtcNow + duration;
    }

    public void ReloadMappings()
    {
        _config = _configService.Load();
        RegisterEncoderFunctions();
        _needsFullRefresh = true;
        _logger.LogInformation("Mappings neu geladen.");
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VoicemeeterBridge gestartet.");

        RegisterCallbacks();

        _logger.LogDebug("Warte auf Voicemeeter- und X-Touch-Verbindung...");
        while (!stoppingToken.IsCancellationRequested && (!_vm.IsConnected || !_xtouch.IsConnected))
        {
            await Task.Delay(200, stoppingToken);
        }

        if (stoppingToken.IsCancellationRequested) return;

        await Task.Delay(500, stoppingToken);
        _ = _vm.IsParameterDirty; // Consume initial dirty snapshot.
        _needsFullRefresh = true;
        _xtouchReconnected = false; // Initial connect is not treated as reconnect.
        _logger.LogInformation("Voicemeeter und X-Touch verbunden — starte initialen Sync.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_xtouchReconnected)
                {
                    _xtouchReconnected = false;
                    _needsFullRefresh = true;
                    _ = _vm.IsParameterDirty; // Consume dirty flag before forced refresh.
                    _logger.LogInformation("X-Touch (re)connected — erzwinge Full Refresh.");
                    await Task.Delay(300, stoppingToken); // Let device state settle before syncing.
                }

                if (!_xtouch.IsConnected)
                {
                    await Task.Delay(500, stoppingToken);
                    continue;
                }

                UpdateLevels();

                if (_needsFullRefresh || _vm.IsParameterDirty)
                {
                    UpdateParameters();
                    _needsFullRefresh = false;
                }

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


    private ControlMappingConfig? GetMapping(int vmChannel)
    {
        _config.Mappings.TryGetValue(vmChannel, out var mapping);
        return mapping;
    }

    private ButtonMappingConfig? GetButtonMapping(int vmChannel, XTouchButtonType buttonType)
    {
        var mapping = GetMapping(vmChannel);
        if (mapping == null) return null;

        string btnKey = buttonType.ToString();
        mapping.Buttons.TryGetValue(btnKey, out var btnMap);
        return btnMap;
    }

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
        }

        if (_config.Channels.TryGetValue(vmChannel, out var chConfig))
            return chConfig.Name;

        return $"Ch {vmChannel + 1}";
    }

    private int MidiDevice_ChannelCount() => Math.Min(_xtouch.ChannelCount, CurrentChannelMapping.Length);
}

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
            catch { /* Scheduled task errors are intentionally ignored. */ }
        }
    }

    public void Clear() => _tasks.Clear();
}



