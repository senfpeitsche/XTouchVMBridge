using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Hardware;
using Microsoft.Extensions.Logging;

namespace XTouchVMBridge.Midi.XTouch;

/// <summary>
/// Output-Methoden: MIDI-Daten an X-Touch senden.
/// Fader, Button-LEDs, Encoder-Ringe, Level-Meter, Display-Text/Farben, 7-Segment-Display.
/// </summary>
public partial class XTouchDevice
{
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
            HandleConnectionLost();
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
            HandleConnectionLost();
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

        // 3. Alle Button-LEDs aus (Channel-Buttons 0–31 + Master-Section 40–103)
        for (int note = 0; note < 32; note++)
            SendShortMessage(0x90, (byte)note, 0);
        for (int note = 40; note <= 103; note++)
            SendShortMessage(0x90, (byte)note, 0);

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
}
