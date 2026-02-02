using XTouchVMBridge.Core.Events;
using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace XTouchVMBridge.App.Services;

/// <summary>
/// Anzeige-Modi für das 7-Segment-Display.
/// </summary>
public enum SegmentDisplayMode
{
    Time,       // Uhrzeit HH.MM.SS
    Date,       // Datum dd.MM.YYYY
    CpuUsage,   // CPU-Last in %
    Off         // Display aus
}

/// <summary>
/// BackgroundService der das 7-Segment-Display auf dem X-Touch steuert.
/// Standardmäßig wird die Uhrzeit angezeigt. Per konfigurierbarem Button
/// kann zwischen verschiedenen Anzeige-Modi gewechselt werden.
/// </summary>
public class SegmentDisplayService : BackgroundService
{
    private readonly ILogger<SegmentDisplayService> _logger;
    private readonly IMidiDevice _midiDevice;
    private readonly XTouchVMBridgeConfig _config;

    private SegmentDisplayMode _currentMode = SegmentDisplayMode.Time;
    private readonly SegmentDisplayMode[] _modes;
    private int _currentModeIndex;

    /// <summary>
    /// MIDI-Note die zum Durchschalten der Anzeige-Modi verwendet wird.
    /// Default: 113 (SMPTE-Button auf dem X-Touch).
    /// </summary>
    public int CycleButtonNote { get; set; } = 113;

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

        // Cycle-Button aus Config lesen (falls vorhanden)
        if (_config.SegmentDisplayCycleButton > 0)
            CycleButtonNote = _config.SegmentDisplayCycleButton;

        _midiDevice.MasterButtonChanged += OnMasterButtonChanged;
        _midiDevice.ConnectionStateChanged += OnConnectionStateChanged;

        _logger.LogInformation("SegmentDisplayService initialisiert (Cycle-Button: Note {Note}, Modus: {Mode}).",
            CycleButtonNote, _currentMode);
    }

    private void OnMasterButtonChanged(object? sender, MasterButtonEventArgs e)
    {
        if (!e.IsPressed) return;
        if (e.NoteNumber != CycleButtonNote) return;

        // Zum nächsten Modus wechseln
        _currentModeIndex = (_currentModeIndex + 1) % _modes.Length;
        _currentMode = _modes[_currentModeIndex];

        _logger.LogInformation("Segment-Display Modus gewechselt zu: {Mode}", _currentMode);

        // Sofort aktualisieren
        UpdateDisplay();
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        if (connected)
        {
            _logger.LogDebug("X-Touch verbunden — Segment-Display wird aktualisiert.");
            // Kurz warten damit das Gerät bereit ist
            Task.Delay(500).ContinueWith(_ => UpdateDisplay());
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("SegmentDisplayService gestartet.");

        // Warte kurz damit X-Touch initialisiert ist
        await Task.Delay(2000, stoppingToken);

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

            // Update-Intervall abhängig vom Modus
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
            SegmentDisplayMode.Off => "            ", // 12 Leerzeichen
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

    /// <summary>Formatiert die aktuelle Uhrzeit als "  HH.MM.SS  " (12 Zeichen).</summary>
    private static string FormatTime()
    {
        var now = DateTime.Now;
        // Format: "  HH.MM.SS  " — Punkte werden als Dots gerendert
        return $"  {now:HH}.{now:mm}.{now:ss}  ";
    }

    /// <summary>Formatiert das aktuelle Datum als "  dd.MM.YYYY" (12 Zeichen).</summary>
    private static string FormatDate()
    {
        var now = DateTime.Now;
        return $"  {now:dd}.{now:MM}.{now:yyyy}";
    }

    /// <summary>Formatiert die CPU-Auslastung als "CPU   42.5 " (12 Zeichen).</summary>
    private static string FormatCpuUsage()
    {
        // Einfache Implementierung ohne PerformanceCounter (der ist langsam beim Start)
        // Zeigt stattdessen GC-Speicher an
        var memMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
        var text = $"  {memMb:F0} Mb    ";
        return text.Length > 12 ? text[..12] : text.PadRight(12);
    }

    public override void Dispose()
    {
        _midiDevice.MasterButtonChanged -= OnMasterButtonChanged;
        _midiDevice.ConnectionStateChanged -= OnConnectionStateChanged;
        base.Dispose();
    }
}
