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
/// Real-time MIDI monitor window with capture and filter controls.
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
        LocalizationService.LocalizeWindow(this);
        _device = device;
        MessageList.ItemsSource = _filteredMessages;

        if (_device != null)
        {
            SubscribeToDevice();
            StatusText.Text = _device.IsConnected
                ? LocalizationService.T("Verbunden — empfange MIDI", "Connected — receiving MIDI")
                : LocalizationService.T("Gerät nicht verbunden", "Device not connected");
        }
        else
        {
            StatusText.Text = LocalizationService.T("Kein MIDI-Gerät zugewiesen", "No MIDI device assigned");
        }
    }


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


    private void OnRawMidiReceived(object? sender, MidiMessageEventArgs e)
    {
        if (e.Data.Length < 3) return;

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
    }

    private void OnEncoderRotated(object? sender, EncoderEventArgs e)
    {
    }

    private void OnEncoderPressed(object? sender, EncoderPressEventArgs e)
    {
    }

    private void OnButtonChanged(object? sender, ButtonEventArgs e)
    {
    }

    private void OnFaderTouched(object? sender, FaderTouchEventArgs e)
    {
    }


    public void LogOutgoingMessage(int rawMessage)
    {
        if (CaptureOutgoing?.IsChecked != true) return;
        var decoded = MidiMessageDecoder.DecodeOutgoing(rawMessage);
        AddMessage(MidiDebugEntry.FromDecoded(decoded));
    }

    public void LogOutgoingSysEx(byte[] data)
    {
        if (CaptureSysEx?.IsChecked != true) return;
        var decoded = MidiMessageDecoder.DecodeSysEx(data, MidiMessageDecoder.MidiDirection.Out);
        AddMessage(MidiDebugEntry.FromDecoded(decoded));
    }


    private void AddMessage(MidiDebugEntry entry)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            lock (_lock)
            {
                _allMessages.Add(entry);
                _totalCount++;

                while (_allMessages.Count > MaxMessages)
                    _allMessages.RemoveAt(0);

                if (MatchesFilter(entry))
                {
                    _filteredMessages.Add(entry);
                    while (_filteredMessages.Count > MaxMessages)
                        _filteredMessages.RemoveAt(0);
                }

                MessageCountText.Text = $"{_totalCount} {LocalizationService.T("Nachrichten", "messages")} ({_filteredMessages.Count} {LocalizationService.T("sichtbar", "visible")})";
            }

            if (AutoScrollCheck.IsChecked == true && _filteredMessages.Count > 0)
            {
                MessageList.ScrollIntoView(_filteredMessages[^1]);
            }
        });
    }

    private bool MatchesFilter(MidiDebugEntry entry)
    {
        if (entry.Direction == MidiMessageDecoder.MidiDirection.In && CaptureIncoming?.IsChecked != true)
            return false;
        if (entry.Direction == MidiMessageDecoder.MidiDirection.Out && CaptureOutgoing?.IsChecked != true)
            return false;

        if (!IsAllValue(_controlTypeFilter))
        {
            bool matchesType = _controlTypeFilter switch
            {
                "Display" => entry.ControlType.Contains("Display") || entry.ControlType.Contains("LCD") || entry.ControlType.Contains("Farben"),
                "SysEx" => entry.ControlType.Contains("SysEx") || entry.ControlType.Contains("Segment") || entry.ControlType.Contains("LCD"),
                _ => entry.ControlType.Contains(_controlTypeFilter, StringComparison.OrdinalIgnoreCase)
            };
            if (!matchesType) return false;
        }

        if (!IsAllValue(_channelFilter))
        {
            bool matchesGerman = entry.ControlId.Contains($"Kanal {_channelFilter}", StringComparison.OrdinalIgnoreCase);
            bool matchesEnglish = entry.ControlId.Contains($"Channel {_channelFilter}", StringComparison.OrdinalIgnoreCase);
            if (!matchesGerman && !matchesEnglish)
                return false;
        }

        return true;
    }

    private void ReapplyFilter()
    {
        if (MessageCountText == null) return;

        lock (_lock)
        {
            _filteredMessages.Clear();
            foreach (var msg in _allMessages)
            {
                if (MatchesFilter(msg))
                    _filteredMessages.Add(msg);
            }
            MessageCountText.Text = $"{_totalCount} {LocalizationService.T("Nachrichten", "messages")} ({_filteredMessages.Count} {LocalizationService.T("sichtbar", "visible")})";
        }
    }

    private static bool IsAllValue(string? value)
    {
        return string.Equals(value, "Alle", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "All", StringComparison.OrdinalIgnoreCase);
    }


    private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ControlTypeFilter?.SelectedItem is ComboBoxItem typeItem)
            _controlTypeFilter = typeItem.Content?.ToString() ?? LocalizationService.T("Alle", "All");

        if (ChannelFilter?.SelectedItem is ComboBoxItem chItem)
            _channelFilter = chItem.Content?.ToString() ?? LocalizationService.T("Alle", "All");

        ReapplyFilter();
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        lock (_lock)
        {
            _allMessages.Clear();
            _filteredMessages.Clear();
            _totalCount = 0;
            MessageCountText.Text = LocalizationService.T("0 Nachrichten", "0 messages");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        UnsubscribeFromDevice();
        base.OnClosed(e);
    }
}

/// <summary>
/// View model entry for one decoded MIDI message row.
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


    public string DirectionText => Direction == MidiMessageDecoder.MidiDirection.In ? "IN" : "OUT";

    public string DirectionColor => Direction == MidiMessageDecoder.MidiDirection.In
        ? "#4EC9B0"  // Green for incoming
        : "#CE9178"; // Orange for outgoing

    public string ControlColor => ControlType switch
    {
        "Button" => "#569CD6",          // Blue
        "Button LED" => "#569CD6",
        "Fader" => "#4EC9B0",           // Green
        "Fader Touch" => "#4EC9B0",
        "Fader (MIDI-Mode)" => "#4EC9B0",
        "Encoder" => "#DCDCAA",         // Yellow
        "Encoder Press" => "#DCDCAA",
        "Encoder Ring" => "#DCDCAA",
        "Level Meter" => "#D7BA7D",     // Gold
        "Meter LED" => "#D7BA7D",
        "LCD Display" => "#C586C0",     // Purple
        "Display Text" => "#C586C0",
        "Display Farben" => "#C586C0",
        "Foot Switch" => "#9CDCFE",     // Light blue
        "Foot Controller" => "#9CDCFE",
        "Jog Wheel" => "#CE9178",       // Orange
        "SysEx" => "#6A9955",           // Green
        "Control Change" => "#858585",  // Gray
        _ => "#D4D4D4"                  // White
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




