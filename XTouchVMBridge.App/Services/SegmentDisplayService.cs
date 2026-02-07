using XTouchVMBridge.Core.Enums;
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
///
/// NAME/VALUE-Button (Note 52) = Cycle-Button (konfiguierbar).
/// Auf dem X-Touch ist der Button "Display Name" doppelt belegt.
///
/// LED-Feedback:
///   - Button-LED leuchtet wenn ein nicht-Standard-Modus aktiv ist (Date, CpuUsage, Off).
///   - Button-LED blinkt kurz beim Moduswechsel als visuelle Bestätigung.
/// </summary>
public class SegmentDisplayService : BackgroundService
{
    private readonly ILogger<SegmentDisplayService> _logger;
    private readonly IMidiDevice _midiDevice;
    private readonly XTouchVMBridgeConfig _config;

    private volatile SegmentDisplayMode _currentMode = SegmentDisplayMode.Time;
    private readonly SegmentDisplayMode[] _modes;
    private volatile int _currentModeIndex;

    /// <summary>
    /// MIDI-Note die zum Durchschalten der Anzeige-Modi verwendet wird.
    /// Default: 52 (NAME/VALUE-Button auf dem X-Touch).
    /// </summary>
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

        // Cycle-Button aus Config lesen (falls vorhanden)
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

    /// <summary>
    /// Fängt auch reguläre ButtonChanged-Events ab, falls der Cycle-Button
    /// im Channel-Button-Bereich liegt (Note &lt; 32).
    /// Normalerweise nicht nötig, aber als Fallback.
    /// </summary>
    private void OnButtonChanged(object? sender, ButtonEventArgs e)
    {
        if (!e.IsPressed) return;

        // ButtonChanged liefert Channel (0-7) und ButtonType
        // Die Note-Nummer muss rekonstruiert werden
        int noteNumber = e.Channel + ((int)e.ButtonType * 8);
        if (noteNumber != CycleButtonNote) return;

        _logger.LogDebug("SegmentDisplay: ButtonChanged als Cycle-Button erkannt — Note={Note}", noteNumber);
        CycleMode();
    }

    private void CycleMode()
    {
        // Zum nächsten Modus wechseln
        _currentModeIndex = (_currentModeIndex + 1) % _modes.Length;
        _currentMode = _modes[_currentModeIndex];

        _logger.LogInformation("Segment-Display Modus gewechselt zu: {Mode} (Index {Index}/{Total})",
            _currentMode, _currentModeIndex + 1, _modes.Length);

        // LED-Feedback basierend auf aktivem Modus
        UpdateCycleButtonLed();

        // Sofort aktualisieren
        UpdateDisplay();
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        if (connected)
        {
            _logger.LogDebug("X-Touch verbunden — Segment-Display wird aktualisiert.");
            // Kurz warten damit das Gerät bereit ist, dann Display + LED setzen
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

        // Warte kurz damit X-Touch initialisiert ist
        await Task.Delay(2000, stoppingToken);

        // Initial LED-Zustand setzen
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

    /// <summary>Setzt die LED des Cycle-Buttons basierend auf dem aktiven Modus.</summary>
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
        _midiDevice.ButtonChanged -= OnButtonChanged;
        _midiDevice.ConnectionStateChanged -= OnConnectionStateChanged;
        base.Dispose();
    }
}
