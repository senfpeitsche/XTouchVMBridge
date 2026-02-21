using XTouchVMBridge.Core.Enums;
using XTouchVMBridge.Core.Hardware;
using Microsoft.Extensions.Logging;

namespace XTouchVMBridge.Midi.XTouch;

public partial class XTouchDevice
{
    public void SetFader(int channel, int position)
    {
        if (channel < 0 || channel > MackieProtocol.ChannelCount)
            throw new ArgumentOutOfRangeException(nameof(channel), $"Channel must be 0-{MackieProtocol.ChannelCount}.");

        if (channel < MackieProtocol.ChannelCount)
            _channels[channel].Fader.Position = position;

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

        int position = value % 16;
        enc.RingPosition = position;

        SendShortMessage(0xB0, (byte)(MackieProtocol.CcEncoderRingBase + channel), (byte)value);
    }

    public void SetLevelMeter(int channel, int level)
    {
        ValidateChannel(channel);
        _channels[channel].LevelMeter.Level = level;

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

        int offset = MackieProtocol.GetDisplayOffset(channel, row);
        var sysex = MackieProtocol.BuildDisplayTextMessage(offset, padded);
        SendSysEx(sysex);
    }

    public void SetDisplayColor(int channel, XTouchColor color)
    {
        ValidateChannel(channel);
        _channels[channel].Display.Color = color;

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
        var ccValues = MackieProtocol.TextToSegmentCcValues(text);

        for (int i = 0; i < MackieProtocol.SegmentDigitCount; i++)
        {
            int cc = MackieProtocol.CcSegmentDisplayBase + (MackieProtocol.SegmentDigitCount - 1 - i);
            SendShortMessage(0xB0, (byte)cc, ccValues[i]);
        }
    }


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

        var handshake = new byte[] { 0xF0, 0x00, 0x00, 0x66, 0x14, 0x13, 0x00, 0xF7 };
        SendSysEx(handshake);

        for (int ch = 0; ch < MackieProtocol.ChannelCount; ch++)
        {
            SetFader(ch, 4384 - 8192); // ca. Mitte
        }

        for (int ch = 0; ch < MackieProtocol.ChannelCount; ch++)
        {
            SendShortMessage(0xB0, (byte)(MackieProtocol.CcEncoderRingBase + ch), 0);
        }

        for (int note = 0; note < 32; note++)
            SendShortMessage(0x90, (byte)note, 0);
        for (int note = 40; note <= 103; note++)
            SendShortMessage(0x90, (byte)note, 0);

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

        string emptyDisplay = new(' ', MackieProtocol.TotalDisplayChars);
        var textSysex = MackieProtocol.BuildDisplayTextMessage(0, emptyDisplay);
        SendSysEx(textSysex);

        _logger.LogDebug("X-Touch initialisiert (Mackie Control Protocol 0x14).");
    }
}
