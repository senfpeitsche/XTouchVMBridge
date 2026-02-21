using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Events;
using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace XTouchVMBridge.App.Services;

/// <summary>
/// Cycles and renders the 12-character segment display content on the X-Touch.
/// </summary>
public enum SegmentDisplayMode
{
    Time,       // Time HH.MM.SS
    Date,       // Date dd.MM.YYYY
    CpuUsage,   // Memory usage in MB (legacy label kept for compatibility)
    Off         // Display off
}

/// <summary>
/// Background worker that refreshes segment display text and cycle-button LED state.
/// </summary>
public class SegmentDisplayService : BackgroundService
{
    private readonly ILogger<SegmentDisplayService> _logger;
    private readonly IMidiDevice _midiDevice;
    private readonly XTouchVMBridgeConfig _config;

    private volatile SegmentDisplayMode _currentMode = SegmentDisplayMode.Time;
    private readonly SegmentDisplayMode[] _modes;
    private volatile int _currentModeIndex;

    public int CycleButtonNote { get; set; } = 52;

    public SegmentDisplayService(
        ILogger<SegmentDisplayService> logger,
        IMidiDevice midiDevice,
        XTouchVMBridgeConfig config)
    {
        _logger = logger;
        _midiDevice = midiDevice;
        _config = config;

        _modes = Enum.GetValues<SegmentDisplayMode>();
        _currentModeIndex = 0;
        _currentMode = _modes[0];

        if (_config.SegmentDisplayCycleButton > 0)
            CycleButtonNote = _config.SegmentDisplayCycleButton;

        _midiDevice.MasterButtonChanged += OnMasterButtonChanged;
        _midiDevice.ButtonChanged += OnButtonChanged;
        _midiDevice.ConnectionStateChanged += OnConnectionStateChanged;

        _logger.LogInformation("SegmentDisplayService initialisiert (Cycle-Button: Note {Note}, Modus: {Mode}).",
            CycleButtonNote, _currentMode);
    }

    private void OnMasterButtonChanged(object? sender, MasterButtonEventArgs e)
    {
        _logger.LogDebug("SegmentDisplay: MasterButtonChanged empfangen — Note={Note}, Pressed={Pressed}",
            e.NoteNumber, e.IsPressed);

        if (!e.IsPressed) return;
        if (e.NoteNumber != CycleButtonNote) return;

        CycleMode();
    }

    private void OnButtonChanged(object? sender, ButtonEventArgs e)
    {
        if (!e.IsPressed) return;

        int noteNumber = e.Channel + ((int)e.ButtonType * 8);
        if (noteNumber != CycleButtonNote) return;

        _logger.LogDebug("SegmentDisplay: ButtonChanged als Cycle-Button erkannt — Note={Note}", noteNumber);
        CycleMode();
    }

    private void CycleMode()
    {
        _currentModeIndex = (_currentModeIndex + 1) % _modes.Length;
        _currentMode = _modes[_currentModeIndex];

        _logger.LogInformation("Segment-Display Modus gewechselt zu: {Mode} (Index {Index}/{Total})",
            _currentMode, _currentModeIndex + 1, _modes.Length);

        UpdateCycleButtonLed();

        UpdateDisplay();
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        if (connected)
        {
            _logger.LogDebug("X-Touch verbunden — Segment-Display wird aktualisiert.");
            Task.Delay(500).ContinueWith(_ =>
            {
                UpdateDisplay();
                UpdateCycleButtonLed();
            });
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("SegmentDisplayService gestartet.");

        await Task.Delay(2000, stoppingToken);

        UpdateCycleButtonLed();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_midiDevice.IsConnected)
                    UpdateDisplay();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Aktualisieren des Segment-Displays.");
            }

            var interval = _currentMode switch
            {
                SegmentDisplayMode.Time => TimeSpan.FromMilliseconds(500),
                SegmentDisplayMode.Date => TimeSpan.FromSeconds(10),
                SegmentDisplayMode.CpuUsage => TimeSpan.FromSeconds(2),
                SegmentDisplayMode.Off => TimeSpan.FromSeconds(5),
                _ => TimeSpan.FromSeconds(1)
            };

            await Task.Delay(interval, stoppingToken);
        }
    }

    private void UpdateDisplay()
    {
        string text = _currentMode switch
        {
            SegmentDisplayMode.Time => FormatTime(),
            SegmentDisplayMode.Date => FormatDate(),
            SegmentDisplayMode.CpuUsage => FormatCpuUsage(),
            SegmentDisplayMode.Off => "            ", // 12 spaces
            _ => FormatTime()
        };

        try
        {
            _midiDevice.SetSegmentDisplay(text);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Segment-Display Update fehlgeschlagen.");
        }
    }

    private void UpdateCycleButtonLed()
    {
        try
        {
            var ledState = _currentMode != SegmentDisplayMode.Time
                ? LedState.On
                : LedState.Off;
            _logger.LogDebug("Cycle-Button LED setzen: Note={Note}, State={State}", CycleButtonNote, ledState);
            _midiDevice.SetMasterButtonLed(CycleButtonNote, ledState);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cycle-Button LED Update fehlgeschlagen.");
        }
    }

    private static string FormatTime()
    {
        var now = DateTime.Now;
        return $"  {now:HH}.{now:mm}.{now:ss}  ";
    }

    private static string FormatDate()
    {
        var now = DateTime.Now;
        return $"  {now:dd}.{now:MM}.{now:yyyy}";
    }

    private static string FormatCpuUsage()
    {
        var memMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
        var text = $"  {memMb:F0} Mb    ";
        return text.Length > 12 ? text[..12] : text.PadRight(12);
    }

    public override void Dispose()
    {
        _midiDevice.MasterButtonChanged -= OnMasterButtonChanged;
        _midiDevice.ButtonChanged -= OnButtonChanged;
        _midiDevice.ConnectionStateChanged -= OnConnectionStateChanged;
        base.Dispose();
    }
}
