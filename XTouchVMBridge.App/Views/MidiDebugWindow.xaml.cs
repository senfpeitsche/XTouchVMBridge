using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using XTouchVMBridge.App.Services;
using XTouchVMBridge.Core.Events;
using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Midi.XTouch;

namespace XTouchVMBridge.App.Views;

/// <summary>
/// Debug-Fenster das alle ein- und ausgehenden MIDI-Nachrichten des X-Touch anzeigt.
/// Zeigt für jede Nachricht: Zeitstempel, Richtung, Control-Typ, Kanal/ID, Wert,
/// die daraus resultierende Aktion, und die rohen MIDI-Bytes.
///
/// Basiert auf der offiziellen Behringer X-Touch MIDI Mode Dokumentation.
/// </summary>
public partial class MidiDebugWindow : Window
{
    private readonly IMidiDevice? _device;
    private readonly ObservableCollection<MidiDebugEntry> _allMessages = new();
    private readonly ObservableCollection<MidiDebugEntry> _filteredMessages = new();
    private readonly object _lock = new();
    private int _totalCount;
    private string _controlTypeFilter = "Alle";
    private string _channelFilter = "Alle";

    private const int MaxMessages = 5000;

    public MidiDebugWindow() : this(null) { }

    public MidiDebugWindow(IMidiDevice? device)
    {
        InitializeComponent();
        Icon = AppIconFactory.CreateWindowIcon();
        _device = device;
        MessageList.ItemsSource = _filteredMessages;

        if (_device != null)
        {
            SubscribeToDevice();
            StatusText.Text = _device.IsConnected
                ? "Verbunden — empfange MIDI"
                : "Gerät nicht verbunden";
        }
        else
        {
            StatusText.Text = "Kein MIDI-Gerät zugewiesen";
        }
    }

    // ─── Device-Events abonnieren ───────────────────────────────────

    private void SubscribeToDevice()
    {
        if (_device == null) return;

        _device.RawMidiReceived += OnRawMidiReceived;
        _device.FaderChanged += OnFaderChanged;
        _device.EncoderRotated += OnEncoderRotated;
        _device.EncoderPressed += OnEncoderPressed;
        _device.ButtonChanged += OnButtonChanged;
        _device.FaderTouched += OnFaderTouched;
    }

    private void UnsubscribeFromDevice()
    {
        if (_device == null) return;

        _device.RawMidiReceived -= OnRawMidiReceived;
        _device.FaderChanged -= OnFaderChanged;
        _device.EncoderRotated -= OnEncoderRotated;
        _device.EncoderPressed -= OnEncoderPressed;
        _device.ButtonChanged -= OnButtonChanged;
        _device.FaderTouched -= OnFaderTouched;
    }

    // ─── Eingehende MIDI-Nachrichten ────────────────────────────────

    private void OnRawMidiReceived(object? sender, MidiMessageEventArgs e)
    {
        if (e.Data.Length < 3) return;

        // UI-Zugriff muss auf dem Dispatcher-Thread erfolgen
        Dispatcher.BeginInvoke(() =>
        {
            if (CaptureIncoming?.IsChecked != true) return;

            int rawMsg = e.Data[0] | (e.Data[1] << 8) | (e.Data[2] << 16);
            var decoded = MidiMessageDecoder.DecodeIncoming(rawMsg);
            AddMessage(MidiDebugEntry.FromDecoded(decoded));
        });
    }

    private void OnFaderChanged(object? sender, FaderEventArgs e)
    {
        // Bereits via RawMidiReceived erfasst — hier nur die Aktion ergänzen
    }

    private void OnEncoderRotated(object? sender, EncoderEventArgs e)
    {
        // Bereits via RawMidiReceived erfasst
    }

    private void OnEncoderPressed(object? sender, EncoderPressEventArgs e)
    {
        // Bereits via RawMidiReceived erfasst
    }

    private void OnButtonChanged(object? sender, ButtonEventArgs e)
    {
        // Bereits via RawMidiReceived erfasst
    }

    private void OnFaderTouched(object? sender, FaderTouchEventArgs e)
    {
        // Bereits via RawMidiReceived erfasst
    }

    // ─── Ausgehende MIDI-Nachrichten loggen (Publik für externen Aufruf) ──

    /// <summary>
    /// Loggt eine ausgehende MIDI-Nachricht (Short Message) im Debug-Fenster.
    /// Wird vom XTouchDevice aufgerufen wenn es Nachrichten sendet.
    /// </summary>
    public void LogOutgoingMessage(int rawMessage)
    {
        if (CaptureOutgoing?.IsChecked != true) return;
        var decoded = MidiMessageDecoder.DecodeOutgoing(rawMessage);
        AddMessage(MidiDebugEntry.FromDecoded(decoded));
    }

    /// <summary>
    /// Loggt eine ausgehende SysEx-Nachricht im Debug-Fenster.
    /// </summary>
    public void LogOutgoingSysEx(byte[] data)
    {
        if (CaptureSysEx?.IsChecked != true) return;
        var decoded = MidiMessageDecoder.DecodeSysEx(data, MidiMessageDecoder.MidiDirection.Out);
        AddMessage(MidiDebugEntry.FromDecoded(decoded));
    }

    // ─── Nachrichten-Verwaltung ─────────────────────────────────────

    private void AddMessage(MidiDebugEntry entry)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            lock (_lock)
            {
                _allMessages.Add(entry);
                _totalCount++;

                // Buffer begrenzen
                while (_allMessages.Count > MaxMessages)
                    _allMessages.RemoveAt(0);

                // Filter anwenden
                if (MatchesFilter(entry))
                {
                    _filteredMessages.Add(entry);
                    while (_filteredMessages.Count > MaxMessages)
                        _filteredMessages.RemoveAt(0);
                }

                MessageCountText.Text = $"{_totalCount} Nachrichten ({_filteredMessages.Count} sichtbar)";
            }

            // Auto-Scroll
            if (AutoScrollCheck.IsChecked == true && _filteredMessages.Count > 0)
            {
                MessageList.ScrollIntoView(_filteredMessages[^1]);
            }
        });
    }

    private bool MatchesFilter(MidiDebugEntry entry)
    {
        // Richtungs-Filter
        if (entry.Direction == MidiMessageDecoder.MidiDirection.In && CaptureIncoming?.IsChecked != true)
            return false;
        if (entry.Direction == MidiMessageDecoder.MidiDirection.Out && CaptureOutgoing?.IsChecked != true)
            return false;

        // Control-Typ-Filter
        if (_controlTypeFilter != "Alle")
        {
            bool matchesType = _controlTypeFilter switch
            {
                "Display" => entry.ControlType.Contains("Display") || entry.ControlType.Contains("LCD") || entry.ControlType.Contains("Farben"),
                "SysEx" => entry.ControlType.Contains("SysEx") || entry.ControlType.Contains("Segment") || entry.ControlType.Contains("LCD"),
                _ => entry.ControlType.Contains(_controlTypeFilter, StringComparison.OrdinalIgnoreCase)
            };
            if (!matchesType) return false;
        }

        // Kanal-Filter
        if (_channelFilter != "Alle")
        {
            if (!entry.ControlId.Contains($"Kanal {_channelFilter}"))
                return false;
        }

        return true;
    }

    private void ReapplyFilter()
    {
        // Während Initialisierung noch nicht filtern
        if (MessageCountText == null) return;

        lock (_lock)
        {
            _filteredMessages.Clear();
            foreach (var msg in _allMessages)
            {
                if (MatchesFilter(msg))
                    _filteredMessages.Add(msg);
            }
            MessageCountText.Text = $"{_totalCount} Nachrichten ({_filteredMessages.Count} sichtbar)";
        }
    }

    // ─── UI-Events ──────────────────────────────────────────────────

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ControlTypeFilter?.SelectedItem is ComboBoxItem typeItem)
            _controlTypeFilter = typeItem.Content?.ToString() ?? "Alle";

        if (ChannelFilter?.SelectedItem is ComboBoxItem chItem)
            _channelFilter = chItem.Content?.ToString() ?? "Alle";

        ReapplyFilter();
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        lock (_lock)
        {
            _allMessages.Clear();
            _filteredMessages.Clear();
            _totalCount = 0;
            MessageCountText.Text = "0 Nachrichten";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        UnsubscribeFromDevice();
        base.OnClosed(e);
    }
}

/// <summary>
/// ViewModel für einen einzelnen Eintrag in der MIDI-Debug-Liste.
/// </summary>
public class MidiDebugEntry
{
    public DateTime Timestamp { get; init; }
    public MidiMessageDecoder.MidiDirection Direction { get; init; }
    public string ControlType { get; init; } = "";
    public string ControlId { get; init; } = "";
    public string Value { get; init; } = "";
    public string Action { get; init; } = "";
    public string RawHex { get; init; } = "";

    // ─── Display-Properties für Data Binding ────────────────────────

    public string DirectionText => Direction == MidiMessageDecoder.MidiDirection.In ? "IN" : "OUT";

    public string DirectionColor => Direction == MidiMessageDecoder.MidiDirection.In
        ? "#4EC9B0"  // Grün für eingehend
        : "#CE9178"; // Orange für ausgehend

    public string ControlColor => ControlType switch
    {
        "Button" => "#569CD6",          // Blau
        "Button LED" => "#569CD6",
        "Fader" => "#4EC9B0",           // Grün
        "Fader Touch" => "#4EC9B0",
        "Fader (MIDI-Mode)" => "#4EC9B0",
        "Encoder" => "#DCDCAA",         // Gelb
        "Encoder Press" => "#DCDCAA",
        "Encoder Ring" => "#DCDCAA",
        "Level Meter" => "#D7BA7D",     // Gold
        "Meter LED" => "#D7BA7D",
        "LCD Display" => "#C586C0",     // Lila
        "Display Text" => "#C586C0",
        "Display Farben" => "#C586C0",
        "Foot Switch" => "#9CDCFE",     // Hellblau
        "Foot Controller" => "#9CDCFE",
        "Jog Wheel" => "#CE9178",       // Orange
        "SysEx" => "#6A9955",           // Grün
        "Control Change" => "#858585",  // Grau
        _ => "#D4D4D4"                  // Weiß
    };

    public static MidiDebugEntry FromDecoded(MidiMessageDecoder.DecodedMidiMessage msg)
    {
        return new MidiDebugEntry
        {
            Timestamp = msg.Timestamp,
            Direction = msg.Direction,
            ControlType = msg.ControlType,
            ControlId = msg.ControlId,
            Value = msg.Value,
            Action = msg.Action,
            RawHex = msg.RawHex
        };
    }

}
