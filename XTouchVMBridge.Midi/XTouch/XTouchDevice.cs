using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Events;
using XTouchVMBridge.Core.Hardware;
using XTouchVMBridge.Core.Interfaces;
using XTouchVMBridge.Core.Models;
using Microsoft.Extensions.Logging;
using NAudio.Midi;

namespace XTouchVMBridge.Midi.XTouch;

/// <summary>
/// Implementierung des X-Touch Extender MIDI-Controllers.
/// Entspricht XTouchLib.py aus dem Python-Original.
///
/// Erweiterbarkeit:
/// - Neue Button-Typen: XTouchButtonType Enum erweitern + NoteBase-Mapping hier ergänzen
/// - Neue Controls: HardwareControlBase ableiten, in XTouchChannel registrieren
/// - Neue Events: EventHandler in IMidiDevice-Interface ergänzen
/// </summary>
public class XTouchDevice : IMidiDevice
{
    private readonly ILogger<XTouchDevice> _logger;
    private readonly XTouchChannel[] _channels;
    private readonly Dictionary<int, DateTime> _noteTimers = new();

    private MidiIn? _midiIn;
    private MidiOut? _midiOut;
    private bool _isConnected;
    private bool _disposed;
    private string? _selectedDeviceName;
    private bool _mainFaderTouched;

    // ─── Events ─────────────────────────────────────────────────────

    public event EventHandler<FaderEventArgs>? FaderChanged;
    public event EventHandler<EncoderEventArgs>? EncoderRotated;
    public event EventHandler<EncoderPressEventArgs>? EncoderPressed;
    public event EventHandler<ButtonEventArgs>? ButtonChanged;
    public event EventHandler<FaderTouchEventArgs>? FaderTouched;
    public event EventHandler<MidiMessageEventArgs>? RawMidiReceived;
    public event EventHandler<MasterButtonEventArgs>? MasterButtonChanged;
    public event EventHandler<bool>? ConnectionStateChanged;

    // ─── Properties ─────────────────────────────────────────────────

    public bool IsConnected => _isConnected;
    public int ChannelCount => MackieProtocol.ChannelCount;
    public IReadOnlyList<XTouchChannel> Channels => _channels;
    public bool IsMainFaderTouched => _mainFaderTouched;

    /// <summary>
    /// Name des gewählten Geräts. Null = automatische Erkennung (erstes X-Touch Gerät).
    /// </summary>
    public string? SelectedDeviceName
    {
        get => _selectedDeviceName;
        set => _selectedDeviceName = value;
    }

    public XTouchDevice(ILogger<XTouchDevice> logger)
    {
        _logger = logger;
        _channels = new XTouchChannel[MackieProtocol.ChannelCount];
        for (int i = 0; i < MackieProtocol.ChannelCount; i++)
            _channels[i] = new XTouchChannel(i);
    }

    // ─── Verbindung ─────────────────────────────────────────────────

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        // Erst bestehende Verbindung trennen
        if (_isConnected || _midiIn != null || _midiOut != null)
        {
            Disconnect();
        }

        var (inputIndex, outputIndex) = FindDevice();
        if (inputIndex < 0 || outputIndex < 0)
        {
            _logger.LogWarning("X-Touch Extender nicht gefunden.");
            _isConnected = false;
            return Task.CompletedTask;
        }

        try
        {
            _midiIn = new MidiIn(inputIndex);
            _midiIn.MessageReceived += OnMidiMessageReceived;
            _midiIn.ErrorReceived += OnMidiError;
            _midiIn.Start();

            _midiOut = new MidiOut(outputIndex);
            _isConnected = true;

            SendInitialization();
            _logger.LogInformation("X-Touch verbunden (In: {Input}, Out: {Output}).",
                MidiIn.DeviceInfo(inputIndex).ProductName,
                MidiOut.DeviceInfo(outputIndex).ProductName);
            ConnectionStateChanged?.Invoke(this, true);
        }
        catch (NAudio.MmException ex) when (ex.Result == NAudio.MmResult.AlreadyAllocated)
        {
            _logger.LogError("X-Touch MIDI-Gerät ist bereits von einer anderen Anwendung geöffnet. " +
                "Bitte schließe andere MIDI-Anwendungen (DAWs, MIDI-Monitore) und starte neu.");
            _isConnected = false;
            ConnectionStateChanged?.Invoke(this, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Verbinden mit X-Touch.");
            _isConnected = false;
            ConnectionStateChanged?.Invoke(this, false);
        }

        return Task.CompletedTask;
    }

    public void Disconnect()
    {
        bool wasConnected = _isConnected;
        _isConnected = false;

        if (_midiIn != null)
        {
            _midiIn.Stop();
            _midiIn.MessageReceived -= OnMidiMessageReceived;
            _midiIn.ErrorReceived -= OnMidiError;
            _midiIn.Dispose();
            _midiIn = null;
        }

        if (_midiOut != null)
        {
            _midiOut.Dispose();
            _midiOut = null;
        }

        _logger.LogInformation("X-Touch getrennt.");
        if (wasConnected)
            ConnectionStateChanged?.Invoke(this, false);
    }

    // ─── Output: Gerät steuern ──────────────────────────────────────

    public void SetFader(int channel, int position)
    {
        // Channels 0-7 are strip faders, channel 8 is the main fader
        if (channel < 0 || channel > MackieProtocol.ChannelCount)
            throw new ArgumentOutOfRangeException(nameof(channel), $"Channel must be 0-{MackieProtocol.ChannelCount}.");

        // Update internal state for strip faders only
        if (channel < MackieProtocol.ChannelCount)
            _channels[channel].Fader.Position = position;

        // Pitchwheel: Status 0xE0+channel, LSB, MSB (14-bit signed → unsigned)
        int unsigned14 = position + 8192;
        byte lsb = (byte)(unsigned14 & 0x7F);
        byte msb = (byte)((unsigned14 >> 7) & 0x7F);
        SendShortMessage(0xE0 + channel, lsb, msb);
    }

    public void SetFaderDb(int channel, double db)
    {
        SetFader(channel, FaderControl.DbToPosition(db));
    }

    public void SetButtonLed(int channel, XTouchButtonType button, LedState state)
    {
        ValidateChannel(channel);
        var btn = _channels[channel].GetButton(button);
        btn.LedState = state;

        byte velocity = state switch
        {
            LedState.Off => MackieProtocol.VelocityOff,
            LedState.Blink => MackieProtocol.VelocityBlink,
            LedState.On => MackieProtocol.VelocityOn,
            _ => MackieProtocol.VelocityOff
        };

        SendShortMessage(0x90, (byte)btn.NoteNumber, velocity);
    }

    public void SetMasterButtonLed(int noteNumber, LedState state)
    {
        byte velocity = state switch
        {
            LedState.Off => MackieProtocol.VelocityOff,
            LedState.Blink => MackieProtocol.VelocityBlink,
            LedState.On => MackieProtocol.VelocityOn,
            _ => MackieProtocol.VelocityOff
        };

        SendShortMessage(0x90, (byte)noteNumber, velocity);
    }

    public void SetEncoderRing(int channel, int value, XTouchEncoderRingMode mode, bool led = false)
    {
        ValidateChannel(channel);
        var enc = _channels[channel].Encoder;
        enc.RingMode = mode;
        enc.RingLed = led;

        // value ist bereits der berechnete CC-Wert (mode * 16 + position [+ 64])
        // Wir extrahieren die Position für den internen State
        int position = value % 16;
        enc.RingPosition = position;

        // Sende den CC-Wert direkt
        SendShortMessage(0xB0, (byte)(MackieProtocol.CcEncoderRingBase + channel), (byte)value);
    }

    public void SetLevelMeter(int channel, int level)
    {
        ValidateChannel(channel);
        _channels[channel].LevelMeter.Level = level;

        // Aftertouch: channel in high nibble, level in low nibble
        int value = (channel << 4) | Math.Clamp(level, 0, MackieProtocol.MaxLevelMeter);
        SendShortMessage(0xD0, (byte)value, 0);
    }

    public void SetDisplayText(int channel, int row, string text)
    {
        ValidateChannel(channel);
        string padded = text.Length >= MackieProtocol.CharsPerChannel
            ? text[..MackieProtocol.CharsPerChannel]
            : text.PadRight(MackieProtocol.CharsPerChannel);

        if (row == 0)
            _channels[channel].Display.TopRow = padded;
        else
            _channels[channel].Display.BottomRow = padded;

        // Mackie Control Format: Offset = channel*7 + row*56
        int offset = MackieProtocol.GetDisplayOffset(channel, row);
        var sysex = MackieProtocol.BuildDisplayTextMessage(offset, padded);
        SendSysEx(sysex);
    }

    public void SetDisplayColor(int channel, XTouchColor color)
    {
        ValidateChannel(channel);
        _channels[channel].Display.Color = color;

        // Sende alle Farben neu (Mackie Protocol erwartet alle 8 auf einmal)
        SendAllDisplayColors();
    }

    public void SetAllDisplayColors(XTouchColor[] colors)
    {
        if (colors.Length != MackieProtocol.ChannelCount)
            throw new ArgumentException($"Genau {MackieProtocol.ChannelCount} Farben erwartet.");

        for (int i = 0; i < MackieProtocol.ChannelCount; i++)
            _channels[i].Display.Color = colors[i];

        SendAllDisplayColors();
    }

    /// <summary>
    /// Sendet alle Display-Farben (Mackie Control Extended Protocol).
    /// </summary>
    private void SendAllDisplayColors()
    {
        byte[] colors = new byte[MackieProtocol.ChannelCount];
        for (int i = 0; i < MackieProtocol.ChannelCount; i++)
            colors[i] = (byte)_channels[i].Display.Color;

        var sysex = MackieProtocol.BuildDisplayColorMessage(colors);
        SendSysEx(sysex);
    }

    public void SetSegmentDisplay(string text)
    {
        // Mackie Control Protocol: CC 64-75 für 12 Digits
        // CC 64 = rechtestes Digit, CC 75 = linkestes Digit (rechts nach links!)
        var ccValues = MackieProtocol.TextToSegmentCcValues(text);

        for (int i = 0; i < MackieProtocol.SegmentDigitCount; i++)
        {
            // Reihenfolge umkehren: ccValues[0] -> CC 75, ccValues[11] -> CC 64
            int cc = MackieProtocol.CcSegmentDisplayBase + (MackieProtocol.SegmentDigitCount - 1 - i);
            SendShortMessage(0xB0, (byte)cc, ccValues[i]);
        }
    }

    /// <summary>
    /// Ermittelt die Behringer Device-ID anhand des verbundenen Gerätenamens.
    /// X-Touch = 0x14, X-Touch Extender = 0x15.
    /// </summary>
    private byte GetBehringerDeviceId()
    {
        var name = _selectedDeviceName ?? "";
        // "X-Touch-Ext" → Extender, alles andere → X-Touch
        if (name.Contains("Ext", StringComparison.OrdinalIgnoreCase))
            return MackieProtocol.DeviceIdXTouchExt;
        return MackieProtocol.DeviceIdXTouch;
    }

    // ─── MIDI Input Callback ────────────────────────────────────────

    private void OnMidiMessageReceived(object? sender, MidiInMessageEventArgs e)
    {
        try
        {
            byte status = (byte)(e.MidiEvent.CommandCode);
            int rawMsg = e.RawMessage;
            byte data1 = (byte)((rawMsg >> 8) & 0xFF);
            byte data2 = (byte)((rawMsg >> 16) & 0xFF);
            byte channel = (byte)(e.MidiEvent.Channel - 1); // NAudio ist 1-basiert

            // Direct Hook — erlaubt externen Code MIDI abzufangen
            var hookArgs = new MidiMessageEventArgs(new[] { (byte)(rawMsg & 0xFF), data1, data2 });
            RawMidiReceived?.Invoke(this, hookArgs);
            if (hookArgs.Handled) return;

            switch (e.MidiEvent.CommandCode)
            {
                case MidiCommandCode.PitchWheelChange:
                    HandleFader(channel, data1, data2);
                    break;

                case MidiCommandCode.ControlChange:
                    HandleEncoder(data1, data2);
                    break;

                case MidiCommandCode.NoteOn:
                    HandleNoteOn(data1, data2);
                    break;

                case MidiCommandCode.NoteOff:
                    HandleNoteOff(data1);
                    break;

                case MidiCommandCode.ChannelAfterTouch:
                    // Level-Meter-Daten vom Gerät (ignorieren wir hier)
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler bei MIDI-Nachrichtenverarbeitung.");
        }
    }

    private void HandleFader(int channel, byte lsb, byte msb)
    {
        // Channels 0-7 are strip faders, channel 8 is the main fader
        if (channel > MackieProtocol.ChannelCount) return;

        int position = ((msb << 7) | lsb) - 8192;
        double db = FaderControl.PositionToDb(position);

        // Update internal state for strip faders (0-7)
        if (channel < MackieProtocol.ChannelCount)
        {
            _channels[channel].Fader.Position = position;
        }

        FaderChanged?.Invoke(this, new FaderEventArgs(channel, position, db));
    }

    private void HandleEncoder(byte cc, byte value)
    {
        int encoderIndex = cc - MackieProtocol.CcEncoderBase;
        if (encoderIndex is < 0 or >= MackieProtocol.ChannelCount) return;

        int ticks = MackieProtocol.DecodeEncoderTicks(value);
        EncoderRotated?.Invoke(this, new EncoderEventArgs(encoderIndex, ticks));
    }

    private void HandleNoteOn(byte note, byte velocity)
    {
        bool isPressed = velocity > 0;

        // Encoder Press (Notes 32–39)
        if (note is >= MackieProtocol.NoteEncoderPressBase and < MackieProtocol.NoteEncoderPressBase + MackieProtocol.ChannelCount)
        {
            int ch = note - MackieProtocol.NoteEncoderPressBase;
            if (isPressed)
            {
                _noteTimers[note] = DateTime.UtcNow;
                _channels[ch].Encoder.IsPressed = true;
            }

            var timePressed = GetTimePressed(note, isPressed);
            if (!isPressed) _channels[ch].Encoder.IsPressed = false;

            EncoderPressed?.Invoke(this, new EncoderPressEventArgs(ch, isPressed, timePressed));
            return;
        }

        // Fader Touch (Notes 110–117 für Channel 0-7, Note 118 für Main Fader)
        if (note is >= MackieProtocol.NoteFaderTouchBase and < MackieProtocol.NoteFaderTouchBase + MackieProtocol.ChannelCount)
        {
            int ch = note - MackieProtocol.NoteFaderTouchBase;
            if (isPressed) _noteTimers[note] = DateTime.UtcNow;

            _channels[ch].Fader.IsTouched = isPressed;
            var timePressed = GetTimePressed(note, isPressed);

            _logger.LogDebug("Fader Touch: Note={Note}, Channel={Ch}, Pressed={Pressed}", note, ch, isPressed);
            FaderTouched?.Invoke(this, new FaderTouchEventArgs(ch, isPressed, timePressed));
            return;
        }

        // Main Fader Touch (Note 118)
        if (note == MackieProtocol.NoteFaderTouchBase + MackieProtocol.ChannelCount)
        {
            if (isPressed) _noteTimers[note] = DateTime.UtcNow;
            _mainFaderTouched = isPressed;
            var timePressed = GetTimePressed(note, isPressed);

            FaderTouched?.Invoke(this, new FaderTouchEventArgs(8, isPressed, timePressed));
            return;
        }

        // Buttons (Notes 0–31: Rec=0–7, Solo=8–15, Mute=16–23, Select=24–31)
        if (note < 32)
        {
            int ch = note % MackieProtocol.ChannelCount;
            var buttonType = (XTouchButtonType)(note / MackieProtocol.ChannelCount);

            if (isPressed) _noteTimers[note] = DateTime.UtcNow;

            var btn = _channels[ch].GetButton(buttonType);
            btn.IsPressed = isPressed;
            var timePressed = GetTimePressed(note, isPressed);

            ButtonChanged?.Invoke(this, new ButtonEventArgs(ch, buttonType, isPressed, timePressed));
        }
        // Master-Section-Buttons (alle nicht-kanalspezifischen Notes: 40+, außer Encoder-Press 32–39 und Fader-Touch 104–111)
        else if (note >= 40)
        {
            if (isPressed) _noteTimers[note] = DateTime.UtcNow;
            var timePressed = GetTimePressed(note, isPressed);
            MasterButtonChanged?.Invoke(this, new MasterButtonEventArgs(note, isPressed));
        }
    }

    private void HandleNoteOff(byte note)
    {
        // NoteOff = NoteOn mit velocity 0
        HandleNoteOn(note, 0);
    }

    private TimeSpan GetTimePressed(byte note, bool isPressed)
    {
        if (isPressed || !_noteTimers.TryGetValue(note, out var startTime))
            return TimeSpan.Zero;

        _noteTimers.Remove(note);
        return DateTime.UtcNow - startTime;
    }

    // ─── MIDI Output Helpers ────────────────────────────────────────

    private void SendShortMessage(int status, byte data1, byte data2)
    {
        if (!_isConnected || _midiOut == null) return;

        try
        {
            int msg = status | (data1 << 8) | (data2 << 16);
            _midiOut.Send(msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Senden der MIDI-Nachricht — Gerät möglicherweise getrennt.");
            _isConnected = false;
            ConnectionStateChanged?.Invoke(this, false);
        }
    }

    private void SendSysEx(byte[] data)
    {
        if (!_isConnected || _midiOut == null) return;

        try
        {
            _midiOut.SendBuffer(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Senden der SysEx-Nachricht — Gerät möglicherweise getrennt.");
            _isConnected = false;
            ConnectionStateChanged?.Invoke(this, false);
        }
    }

    private void SendInitialization()
    {
        // Mackie Control Protocol Initialisierung (wie Python-Original)

        // 0. Handshake senden (wichtig für X-Touch!)
        var handshake = new byte[] { 0xF0, 0x00, 0x00, 0x66, 0x14, 0x13, 0x00, 0xF7 };
        SendSysEx(handshake);

        // 1. Faders auf Mittelposition
        for (int ch = 0; ch < MackieProtocol.ChannelCount; ch++)
        {
            SetFader(ch, 4384 - 8192); // ca. Mitte
        }

        // 2. Encoder-Ringe löschen
        for (int ch = 0; ch < MackieProtocol.ChannelCount; ch++)
        {
            SendShortMessage(0xB0, (byte)(MackieProtocol.CcEncoderRingBase + ch), 0);
        }

        // 3. Alle Button-LEDs aus
        for (int note = 0; note < 32; note++)
        {
            SendShortMessage(0x90, (byte)note, 0);
        }

        // 4. Display-Farben auf Weiß (alle 8 Kanäle)
        byte[] colors = new byte[MackieProtocol.ChannelCount];
        for (int i = 0; i < MackieProtocol.ChannelCount; i++)
        {
            colors[i] = (byte)XTouchColor.White;
            _channels[i].Display.Color = XTouchColor.White;
            _channels[i].Display.TopRow = "       ";
            _channels[i].Display.BottomRow = "       ";
        }
        var colorSysex = MackieProtocol.BuildDisplayColorMessage(colors);
        SendSysEx(colorSysex);

        // 5. Display-Text löschen (kompletter 112-Zeichen Buffer, beide Zeilen)
        string emptyDisplay = new(' ', MackieProtocol.TotalDisplayChars);
        var textSysex = MackieProtocol.BuildDisplayTextMessage(0, emptyDisplay);
        SendSysEx(textSysex);

        _logger.LogDebug("X-Touch initialisiert (Mackie Control Protocol 0x14).");
    }

    // ─── Geräte-Erkennung ───────────────────────────────────────────

    /// <summary>
    /// Listet alle verfügbaren X-Touch MIDI-Geräte auf.
    /// Gibt Gerätenamen zurück, die sowohl als Input als auch Output vorhanden sind.
    /// </summary>
    public IReadOnlyList<string> ListDevices()
    {
        var inputs = new HashSet<string>();
        var outputs = new HashSet<string>();

        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            var name = MidiIn.DeviceInfo(i).ProductName;
            if (name.Contains("X-Touch", StringComparison.OrdinalIgnoreCase))
                inputs.Add(name);
        }

        for (int i = 0; i < MidiOut.NumberOfDevices; i++)
        {
            var name = MidiOut.DeviceInfo(i).ProductName;
            if (name.Contains("X-Touch", StringComparison.OrdinalIgnoreCase))
                outputs.Add(name);
        }

        inputs.IntersectWith(outputs);
        return inputs.OrderBy(n => n).ToList();
    }

    private (int inputIndex, int outputIndex) FindDevice()
    {
        int inputIndex = -1;
        int outputIndex = -1;
        string searchTerm = _selectedDeviceName ?? "X-Touch";

        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            if (MidiIn.DeviceInfo(i).ProductName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                inputIndex = i;
                break;
            }
        }

        for (int i = 0; i < MidiOut.NumberOfDevices; i++)
        {
            if (MidiOut.DeviceInfo(i).ProductName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                outputIndex = i;
                break;
            }
        }

        return (inputIndex, outputIndex);
    }

    // ─── Error Handling ─────────────────────────────────────────────

    private void OnMidiError(object? sender, MidiInMessageEventArgs e)
    {
        _logger.LogWarning("MIDI-Fehler empfangen: {Message}", e.RawMessage);
    }

    private static void ValidateChannel(int channel)
    {
        if (channel < 0 || channel >= MackieProtocol.ChannelCount)
            throw new ArgumentOutOfRangeException(nameof(channel),
                $"Kanal muss zwischen 0 und {MackieProtocol.ChannelCount - 1} liegen.");
    }

    // ─── IDisposable ────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
